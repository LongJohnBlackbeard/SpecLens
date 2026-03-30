using JdeClient.Core.Models;

namespace JdeClient.Core.Internal;

internal sealed partial class EventRulesQueryEngine
{
    public JdeBatchApplicationSpec GetBatchApplicationSpec(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Object name is required.", nameof(objectName));
        }

        var rows = GetEventRulesLinkRows(objectName, ProductTypeRda);
        IReadOnlyDictionary<EventRulesSectionKey, ReportSectionMetadata> sectionMetadata = rows.Count == 0
            ? new Dictionary<EventRulesSectionKey, ReportSectionMetadata>()
            : LoadReportSectionMetadata(objectName, rows);

        IReadOnlyList<JdeBatchVersionSpec> versions = BuildBatchVersionSpecs(objectName, rows, sectionMetadata);

        return new JdeBatchApplicationSpec
        {
            ObjectName = objectName,
            Name = objectName,
            Versions = versions,
            MetadataSections = BuildBatchApplicationMetadataSections(objectName, versions)
        };
    }

    private JdeEventRulesNode BuildBatchApplicationTree(JdeBatchApplicationSpec spec)
    {
        bool showVersionNodes = spec.Versions.Count > 1 ||
                                spec.Versions.Any(version => !string.IsNullOrWhiteSpace(version.VersionName));

        IReadOnlyList<JdeEventRulesNode> children = showVersionNodes
            ? spec.Versions.Select(version => BuildBatchVersionNode(spec.ObjectName, version)).ToList()
            : spec.Versions.SelectMany(version => version.Sections.Select(section => BuildBatchSectionNode(spec.ObjectName, version.VersionName, section)))
                .ToList();

        return new JdeEventRulesNode
        {
            Id = spec.ObjectName,
            Name = spec.ObjectName,
            NodeType = JdeEventRulesNodeType.Object,
            MetadataSections = spec.MetadataSections,
            Children = children
        };
    }

    private IReadOnlyList<JdeBatchVersionSpec> BuildBatchVersionSpecs(
        string objectName,
        IReadOnlyList<EventRulesLinkRow> rows,
        IReadOnlyDictionary<EventRulesSectionKey, ReportSectionMetadata> sectionMetadata)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<JdeBatchVersionSpec>();
        }

        bool hasExplicitVersion = rows.Any(row => !string.IsNullOrWhiteSpace(row.Version));

        return rows
            .GroupBy(row => hasExplicitVersion ? NormalizeReportVersion(row.Version) : string.Empty)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                IReadOnlyList<JdeBatchSectionSpec> sections = group
                    .GroupBy(row => NormalizeReportSectionKey(row.FormName))
                    .OrderBy(sectionGroup => sectionGroup.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(sectionGroup => BuildBatchSectionSpec(
                        objectName,
                        group.Key,
                        sectionGroup.Key,
                        sectionGroup.ToList(),
                        sectionMetadata))
                    .ToList();

                return new JdeBatchVersionSpec
                {
                    VersionName = group.Key,
                    MetadataSections = BuildBatchVersionMetadataSections(group.Key, sections),
                    Sections = sections
                };
            })
            .ToList();
    }

    private JdeBatchSectionSpec BuildBatchSectionSpec(
        string objectName,
        string versionName,
        string sectionKey,
        IReadOnlyList<EventRulesLinkRow> rows,
        IReadOnlyDictionary<EventRulesSectionKey, ReportSectionMetadata> sectionMetadata)
    {
        rows = rows.Count == 0 ? Array.Empty<EventRulesLinkRow>() : rows;
        EventRulesLinkRow? first = rows.FirstOrDefault();

        sectionMetadata.TryGetValue(
            new EventRulesSectionKey(
                first == null ? NormalizeReportVersion(versionName) : NormalizeReportVersion(first.Version),
                sectionKey),
            out ReportSectionMetadata? reportSection);

        int sectionId = reportSection?.SectionId ?? 0;
        if (sectionId == 0)
        {
            TryParseReportSectionIdentifier(sectionKey, out sectionId);
        }

        string sectionName = reportSection?.SectionName ?? NormalizeGroupKey(sectionKey);
        var section = new JdeBatchSectionSpec
        {
            SectionKey = sectionKey,
            Name = sectionName,
            SectionId = sectionId,
            SectionType = reportSection?.SectionType ?? string.Empty,
            BusinessViewName = reportSection?.BusinessViewName,
            Visible = reportSection?.Visible ?? false,
            AbsolutePositional = reportSection?.AbsolutePositional ?? false,
            PageBreakAfter = reportSection?.PageBreakAfter ?? false,
            Conditional = reportSection?.Conditional ?? false,
            ReprintAtPageBreak = reportSection?.ReprintAtPageBreak ?? false,
            Left = reportSection?.Left ?? 0,
            Top = reportSection?.Top ?? 0,
            Width = reportSection?.Width ?? 0,
            Height = reportSection?.Height ?? 0,
            Events = BuildBatchEventSpecs(
                rows.Where(row => row.ControlId == 0).ToList(),
                controlId: 0),
            Controls = BuildBatchControlSpecs(
                rows.Where(row => row.ControlId != 0).ToList())
        };

        section.MetadataSections = BuildBatchSectionMetadataSections(objectName, section);
        return section;
    }

    private IReadOnlyList<JdeBatchControlSpec> BuildBatchControlSpecs(IReadOnlyList<EventRulesLinkRow> rows)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<JdeBatchControlSpec>();
        }

        return rows
            .GroupBy(row => row.ControlId)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var control = new JdeBatchControlSpec
                {
                    ControlId = group.Key,
                    Name = FormatControlLabel(group.Key, ProductTypeRda),
                    Events = BuildBatchEventSpecs(group.ToList(), group.Key)
                };

                control.MetadataSections = BuildBatchControlMetadataSections(control);
                return control;
            })
            .ToList();
    }

    private static IReadOnlyList<JdeBatchEventSpec> BuildBatchEventSpecs(
        IReadOnlyList<EventRulesLinkRow> rows,
        int? controlId)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<JdeBatchEventSpec>();
        }

        return rows
            .GroupBy(row => new { row.EventSpecKey, row.EventId, row.EventOrder, row.Id3 })
            .OrderBy(group => group.Key.EventOrder)
            .ThenBy(group => group.Key.EventId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.Id3)
            .Select(group =>
            {
                EventRulesLinkRow first = group.First();
                var eventSpec = new JdeBatchEventSpec
                {
                    Name = FormatEventLabel(group.Key.EventId, group.Key.Id3, ProductTypeRda, first.FormName),
                    EventSpecKey = group.Key.EventSpecKey,
                    EventId = ToOptional(group.Key.EventId),
                    EventId3 = group.Key.Id3 == 0 ? null : group.Key.Id3,
                    ControlId = controlId
                };

                eventSpec.MetadataSections = BuildBatchEventMetadataSections(eventSpec);
                return eventSpec;
            })
            .ToList();
    }

    private JdeEventRulesNode BuildBatchVersionNode(
        string objectName,
        JdeBatchVersionSpec version)
    {
        return new JdeEventRulesNode
        {
            Id = $"version:{version.VersionName}",
            Name = FormatVersionLabel(version.VersionName),
            NodeType = JdeEventRulesNodeType.Section,
            VersionName = ToOptional(version.VersionName),
            MetadataSections = version.MetadataSections,
            Children = version.Sections.Select(section => BuildBatchSectionNode(objectName, version.VersionName, section)).ToList()
        };
    }

    private JdeEventRulesNode BuildBatchSectionNode(
        string objectName,
        string versionName,
        JdeBatchSectionSpec section)
    {
        var children = new List<JdeEventRulesNode>();
        if (section.Events.Count > 0)
        {
            children.Add(new JdeEventRulesNode
            {
                Id = $"control:{section.SectionKey}:0",
                Name = "Section Events",
                NodeType = JdeEventRulesNodeType.Form,
                VersionName = ToOptional(versionName),
                FormOrSectionName = ToOptional(section.SectionKey),
                ControlId = 0,
                Children = section.Events.Select(eventSpec => BuildBatchEventNode(versionName, section.SectionKey, eventSpec)).ToList()
            });
        }

        children.AddRange(section.Controls.Select(control => BuildBatchControlNode(versionName, section.SectionKey, control)));

        return new JdeEventRulesNode
        {
            Id = $"form:{section.SectionKey}",
            Name = section.SectionId > 0
                ? $"{section.Name} ({section.SectionId})"
                : section.Name,
            NodeType = JdeEventRulesNodeType.Form,
            VersionName = ToOptional(versionName),
            FormOrSectionName = ToOptional(section.SectionKey),
            ComponentTypeName = section.SectionType,
            ComponentConfiguration = BuildReportSectionConfiguration(
                objectName,
                new ReportSectionMetadata(
                    section.Name,
                    section.SectionId,
                    section.SectionType,
                    section.Visible,
                    section.AbsolutePositional,
                    section.PageBreakAfter,
                    section.Conditional,
                    section.ReprintAtPageBreak,
                    section.Left,
                    section.Top,
                    section.Width,
                    section.Height,
                    section.BusinessViewName)),
            MetadataSections = section.MetadataSections,
            Children = children
        };
    }

    private JdeEventRulesNode BuildBatchControlNode(
        string versionName,
        string sectionKey,
        JdeBatchControlSpec control)
    {
        return new JdeEventRulesNode
        {
            Id = $"control:{sectionKey}:{control.ControlId}",
            Name = control.Name,
            NodeType = JdeEventRulesNodeType.Control,
            VersionName = ToOptional(versionName),
            FormOrSectionName = ToOptional(sectionKey),
            ControlId = control.ControlId,
            DataStructureName = control.DataStructureName,
            TextId = control.TextId,
            ComponentTypeName = control.ComponentTypeName,
            ComponentConfiguration = control.ComponentConfiguration,
            MetadataSections = control.MetadataSections,
            Children = control.Events.Select(eventSpec => BuildBatchEventNode(versionName, sectionKey, eventSpec)).ToList()
        };
    }

    private JdeEventRulesNode BuildBatchEventNode(
        string versionName,
        string sectionKey,
        JdeBatchEventSpec eventSpec)
    {
        return new JdeEventRulesNode
        {
            Id = eventSpec.EventSpecKey,
            Name = eventSpec.Name,
            NodeType = JdeEventRulesNodeType.Event,
            EventSpecKey = eventSpec.EventSpecKey,
            VersionName = ToOptional(versionName),
            FormOrSectionName = ToOptional(sectionKey),
            ControlId = eventSpec.ControlId,
            EventId = eventSpec.EventId,
            EventId3 = eventSpec.EventId3,
            MetadataSections = eventSpec.MetadataSections
        };
    }

    private IReadOnlyList<JdeSpecMetadataSection> BuildBatchApplicationMetadataSections(
        string objectName,
        IReadOnlyList<JdeBatchVersionSpec> versions)
    {
        var sections = new List<JdeSpecMetadataSection>();
        int sectionCount = versions.Sum(version => version.Sections.Count);
        int controlCount = versions.Sum(version => version.Sections.Sum(section => section.Controls.Count));
        int eventCount = versions.Sum(version => version.Sections.Sum(section =>
            section.Events.Count + section.Controls.Sum(control => control.Events.Count)));

        AddMetadataSection(
            sections,
            "Properties",
            new[]
            {
                CreateMetadataItem("Object", objectName),
                CreateMetadataItem("Versions", versions.Count),
                CreateMetadataItem("Sections", sectionCount),
                CreateMetadataItem("Controls", controlCount),
                CreateMetadataItem("Events", eventCount)
            });

        return sections;
    }

    private static IReadOnlyList<JdeSpecMetadataSection> BuildBatchVersionMetadataSections(
        string versionName,
        IReadOnlyList<JdeBatchSectionSpec> sections)
    {
        var metadataSections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            metadataSections,
            "Properties",
            new[]
            {
                CreateMetadataItem("Version", string.IsNullOrWhiteSpace(versionName) ? "<default>" : versionName),
                CreateMetadataItem("Sections", sections.Count),
                CreateMetadataItem(
                    "Events",
                    sections.Sum(section => section.Events.Count + section.Controls.Sum(control => control.Events.Count)))
            });

        return metadataSections;
    }

    private static IReadOnlyList<JdeSpecMetadataSection> BuildBatchSectionMetadataSections(
        string objectName,
        JdeBatchSectionSpec section)
    {
        var sections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            sections,
            "Properties",
            new[]
            {
                CreateMetadataItem("Object", objectName),
                CreateMetadataItem("Section", section.Name),
                CreateMetadataItem("Section ID", section.SectionId),
                CreateMetadataItem("Type", section.SectionType),
                CreateMetadataItem("Business View", section.BusinessViewName),
                CreateMetadataItem("Visible", section.Visible),
                CreateMetadataItem("Absolute Positional", section.AbsolutePositional),
                CreateMetadataItem("Page Break After", section.PageBreakAfter),
                CreateMetadataItem("Conditional", section.Conditional),
                CreateMetadataItem("Reprint At Page Break", section.ReprintAtPageBreak),
                CreateMetadataItem("Left", section.Left),
                CreateMetadataItem("Top", section.Top),
                CreateMetadataItem("Width", section.Width),
                CreateMetadataItem("Height", section.Height),
                CreateMetadataItem("Controls", section.Controls.Count),
                CreateMetadataItem("Events", section.Events.Count)
            });

        return sections;
    }

    private static IReadOnlyList<JdeSpecMetadataSection> BuildBatchControlMetadataSections(JdeBatchControlSpec control)
    {
        var sections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            sections,
            "Properties",
            new[]
            {
                CreateMetadataItem("Control ID", control.ControlId),
                CreateMetadataItem("Name", control.Name),
                CreateMetadataItem("Component Type", control.ComponentTypeName),
                CreateMetadataItem("Data Structure", control.DataStructureName),
                CreateMetadataItem("Text ID", control.TextId),
                CreateMetadataItem("Events", control.Events.Count)
            });

        return sections;
    }

    private static IReadOnlyList<JdeSpecMetadataSection> BuildBatchEventMetadataSections(JdeBatchEventSpec eventSpec)
    {
        var sections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            sections,
            "Properties",
            new[]
            {
                CreateMetadataItem("Event", eventSpec.Name),
                CreateMetadataItem("Event Spec Key", eventSpec.EventSpecKey),
                CreateMetadataItem("Event ID", eventSpec.EventId),
                CreateMetadataItem("ID3", eventSpec.EventId3),
                CreateMetadataItem("Control ID", eventSpec.ControlId)
            });

        return sections;
    }
}
