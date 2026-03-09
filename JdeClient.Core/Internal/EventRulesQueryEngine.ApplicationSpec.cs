using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using JdeClient.Core.XmlEngine.Models;
using static JdeClient.Core.Interop.JdeSpecEncapApi;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

internal sealed partial class EventRulesQueryEngine
{
    public JdeInteractiveApplicationSpec GetInteractiveApplicationSpec(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Object name is required.", nameof(objectName));
        }

        var eventRows = GetEventRulesLinkRows(objectName, ProductTypeFda);
        InteractiveApplicationCatalog catalog = LoadInteractiveApplicationCatalog(objectName);

        var textIds = new HashSet<int>();
        if (catalog.ApplicationDescriptor.TextId.HasValue)
        {
            textIds.Add(catalog.ApplicationDescriptor.TextId.Value);
        }

        foreach (ApplicationFormDescriptor descriptor in catalog.Forms.Values)
        {
            if (descriptor.TextId.HasValue)
            {
                textIds.Add(descriptor.TextId.Value);
            }
        }

        foreach (ApplicationControlDescriptor descriptor in catalog.Controls.Values)
        {
            if (descriptor.TextId.HasValue)
            {
                textIds.Add(descriptor.TextId.Value);
            }
        }

        IReadOnlyDictionary<int, string> textById = LoadApplicationTextById(objectName, textIds.ToArray());
        IReadOnlyDictionary<string, string> dataDictionaryTitles = LoadApplicationDataDictionaryTitles(
            catalog.Controls.Values,
            textById);
        var dataStructureCache = new Dictionary<string, JdeInteractiveDataStructureSpec?>(StringComparer.OrdinalIgnoreCase);
        var businessViewCache = new Dictionary<string, JdeBusinessViewInfo?>(StringComparer.OrdinalIgnoreCase);

        using var businessViewService = new SpecBusinessViewMetadataService(_hUser, _options);

        string applicationName = ResolveApplicationName(catalog.ApplicationDescriptor, objectName, textById);
        JdeInteractiveDataStructureSpec? processingOptions = LoadInteractiveDataStructureSpec(
            catalog.ApplicationDescriptor.ProcessingOptionTemplate,
            "Processing Options",
            dataStructureCache);

        IReadOnlyList<JdeInteractiveFormSpec> forms = BuildInteractiveApplicationForms(
            objectName,
            catalog,
                eventRows,
                textById,
                dataDictionaryTitles,
                dataStructureCache,
                businessViewCache,
                businessViewService);

        return new JdeInteractiveApplicationSpec
        {
            ObjectName = objectName,
            Name = applicationName,
            ProcessingOptionTemplateName = catalog.ApplicationDescriptor.ProcessingOptionTemplate,
            ProcessingOptions = processingOptions,
            Forms = forms,
            MetadataSections = BuildInteractiveApplicationMetadataSections(
                objectName,
                applicationName,
                catalog.ApplicationDescriptor,
                processingOptions),
            Attributes = ToAttributeDictionary(catalog.ApplicationDescriptor.Attributes)
        };
    }

    private JdeEventRulesNode BuildInteractiveApplicationTree(JdeInteractiveApplicationSpec spec)
    {
        var children = spec.Forms
            .Select(BuildInteractiveApplicationFormNode)
            .ToList();

        return new JdeEventRulesNode
        {
            Id = spec.ObjectName,
            Name = spec.ObjectName,
            NodeType = JdeEventRulesNodeType.Object,
            DataStructureName = spec.ProcessingOptionTemplateName,
            ComponentConfiguration = string.IsNullOrWhiteSpace(spec.Name)
                ? string.Empty
                : $"Application: {spec.Name}",
            MetadataSections = spec.MetadataSections,
            Children = children
        };
    }

    private JdeEventRulesNode BuildInteractiveApplicationFormNode(JdeInteractiveFormSpec form)
    {
        JdeEventRulesNode? eventsNode = null;
        var componentNodes = new List<JdeEventRulesNode>();
        if (form.Events.Count > 0)
        {
            eventsNode = BuildInteractiveApplicationEventsNode(
                $"events:form:{form.ObjectName}",
                form.ObjectName,
                controlId: null,
                form.DataStructureName,
                form.Events);
        }

        componentNodes.AddRange(form.Components.Select(component => BuildInteractiveApplicationComponentNode(form, component)));

        var children = new List<JdeEventRulesNode>();
        if (eventsNode != null)
        {
            children.Add(eventsNode);
        }

        children.AddRange(componentNodes
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.ControlId ?? int.MaxValue));

        return new JdeEventRulesNode
        {
            Id = $"form:{form.ObjectName}",
            Name = string.IsNullOrWhiteSpace(form.Name)
                ? form.ObjectName
                : $"{form.Name} ({form.ObjectName})",
            NodeType = JdeEventRulesNodeType.Form,
            FormOrSectionName = form.ObjectName,
            DataStructureName = form.DataStructureName,
            ComponentTypeName = form.FormTypeLabel,
            ComponentConfiguration = BuildInteractiveFormSummary(form),
            MetadataSections = form.MetadataSections,
            Children = children
        };
    }

    private JdeEventRulesNode BuildInteractiveApplicationComponentNode(
        JdeInteractiveFormSpec form,
        JdeInteractiveComponentSpec component)
    {
        var children = new List<JdeEventRulesNode>();
        if (component.Events.Count > 0)
        {
            children.Add(BuildInteractiveApplicationEventsNode(
                $"events:control:{form.ObjectName}:{component.ControlId}",
                form.ObjectName,
                component.ControlId,
                component.DataStructureName,
                component.Events));
        }

        children.AddRange(component.Children.Select(child => BuildInteractiveApplicationComponentNode(form, child)));

        return new JdeEventRulesNode
        {
            Id = $"control:{form.ObjectName}:{component.ControlId}",
            Name = component.Name,
            NodeType = JdeEventRulesNodeType.Control,
            FormOrSectionName = form.ObjectName,
            ControlId = component.ControlId,
            DataStructureName = component.DataStructureName,
            ComponentTypeName = component.ComponentTypeLabel,
            ComponentConfiguration = BuildInteractiveComponentSummary(component),
            MetadataSections = component.MetadataSections,
            Children = children
        };
    }

    private static JdeEventRulesNode BuildInteractiveApplicationEventsNode(
        string id,
        string formName,
        int? controlId,
        string? dataStructureName,
        IReadOnlyList<JdeInteractiveEventSpec> events)
    {
        return new JdeEventRulesNode
        {
            Id = id,
            Name = "Events",
            NodeType = JdeEventRulesNodeType.Section,
            FormOrSectionName = formName,
            ControlId = controlId,
            DataStructureName = dataStructureName,
            Children = events
                .Select(eventSpec => new JdeEventRulesNode
                {
                    Id = eventSpec.EventSpecKey,
                    Name = eventSpec.Name,
                    NodeType = JdeEventRulesNodeType.Event,
                    EventSpecKey = eventSpec.EventSpecKey,
                    FormOrSectionName = formName,
                    ControlId = controlId,
                    EventId = eventSpec.EventId,
                    EventId3 = eventSpec.EventId3,
                    DataStructureName = eventSpec.TemplateName,
                    ComponentConfiguration = BuildInteractiveEventSummary(eventSpec),
                    MetadataSections = eventSpec.MetadataSections
                })
                .ToList()
        };
    }

    private IReadOnlyList<JdeInteractiveFormSpec> BuildInteractiveApplicationForms(
        string objectName,
        InteractiveApplicationCatalog catalog,
        IReadOnlyList<EventRulesLinkRow> eventRows,
        IReadOnlyDictionary<int, string> textById,
        IReadOnlyDictionary<string, string> dataDictionaryTitles,
        IDictionary<string, JdeInteractiveDataStructureSpec?> dataStructureCache,
        IDictionary<string, JdeBusinessViewInfo?> businessViewCache,
        SpecBusinessViewMetadataService businessViewService)
    {
        var forms = new List<JdeInteractiveFormSpec>();
        var formNames = catalog.Forms.Keys
            .Concat(eventRows.Select(row => NormalizeFormKey(row.FormName)))
            .Where(formName => !string.IsNullOrWhiteSpace(formName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(formName => formName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string formName in formNames)
        {
            catalog.Forms.TryGetValue(formName, out ApplicationFormDescriptor? descriptor);

            string displayName = ResolveFormDisplayName(formName, descriptor, catalog.FormDescriptions, textById);
            string? formDataStructureName = descriptor?.DataStructureName;

            JdeInteractiveDataStructureSpec? dataStructure = LoadInteractiveDataStructureSpec(
                formDataStructureName,
                descriptor?.FormType == JdeInteractiveFormType.Subform
                    ? "Subform Structure"
                    : "Form Interconnect",
                dataStructureCache);

            JdeBusinessViewInfo? businessView = LoadInteractiveBusinessViewInfo(
                descriptor?.BusinessViewName,
                businessViewCache,
                businessViewService);

            IReadOnlyList<JdeInteractiveEventSpec> events = BuildInteractiveEventSpecs(
                eventRows
                    .Where(row =>
                        string.Equals(NormalizeFormKey(row.FormName), formName, StringComparison.OrdinalIgnoreCase) &&
                        row.ControlId == 0)
                    .ToList(),
                ProductTypeFda,
                displayName,
                componentName: null,
                templateName: formDataStructureName);

            IReadOnlyList<JdeInteractiveComponentSpec> components = BuildInteractiveComponentsForForm(
                formName,
                displayName,
                catalog.Controls,
                eventRows,
                textById,
                dataDictionaryTitles,
                businessViewCache,
                businessViewService);

            var form = new JdeInteractiveFormSpec
            {
                ObjectName = formName,
                Name = displayName,
                FormType = descriptor?.FormType ?? JdeInteractiveFormType.Unknown,
                FormTypeLabel = descriptor?.FormTypeLabel ?? "Unknown",
                BusinessViewName = descriptor?.BusinessViewName,
                BusinessView = businessView,
                UpdateOnFormBusinessView = descriptor?.UpdateOnFormBusinessView,
                FetchOnFormBusinessView = descriptor?.FetchOnFormBusinessView,
                UpdateOnGridBusinessView = descriptor?.UpdateOnGridBusinessView,
                FetchOnGridBusinessView = descriptor?.FetchOnGridBusinessView,
                TransactionType = descriptor?.TransactionType,
                ControlCount = components.Count,
                DataStructureName = formDataStructureName,
                DataStructure = dataStructure,
                Events = events,
                Components = components,
                Attributes = descriptor == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : ToAttributeDictionary(descriptor.Attributes)
            };

            form.MetadataSections = BuildInteractiveFormMetadataSections(form, descriptor);
            forms.Add(form);
        }

        return forms;
    }

    private IReadOnlyList<JdeInteractiveComponentSpec> BuildInteractiveComponentsForForm(
        string normalizedFormName,
        string formDisplayName,
        IReadOnlyDictionary<EventRulesControlKey, ApplicationControlDescriptor> descriptors,
        IReadOnlyList<EventRulesLinkRow> eventRows,
        IReadOnlyDictionary<int, string> textById,
        IReadOnlyDictionary<string, string> dataDictionaryTitles,
        IDictionary<string, JdeBusinessViewInfo?> businessViewCache,
        SpecBusinessViewMetadataService businessViewService)
    {
        var rowsByControl = eventRows
            .Where(row =>
                string.Equals(NormalizeFormKey(row.FormName), normalizedFormName, StringComparison.OrdinalIgnoreCase) &&
                row.ControlId > 0)
            .GroupBy(row => row.ControlId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<EventRulesLinkRow>)group.ToList());

        var controlIds = descriptors.Keys
            .Where(key => string.Equals(key.FormName, normalizedFormName, StringComparison.OrdinalIgnoreCase))
            .Select(key => key.ControlId)
            .Concat(rowsByControl.Keys)
            .Distinct()
            .OrderBy(controlId => controlId)
            .ToList();

        var componentsById = new Dictionary<int, JdeInteractiveComponentSpec>();
        var parentById = new Dictionary<int, int?>();

        foreach (int controlId in controlIds)
        {
            descriptors.TryGetValue(new EventRulesControlKey(normalizedFormName, controlId), out ApplicationControlDescriptor? descriptor);

            string? resolvedText = descriptor == null
                ? null
                : ResolveApplicationControlDisplayText(
                    descriptor.TextId,
                    descriptor.Attributes,
                    textById,
                    dataDictionaryTitles);

            string name = descriptor == null
                ? FormatControlLabel(controlId, ProductTypeFda, metadata: null)
                : BuildApplicationControlLabel(controlId, descriptor, resolvedText);

            JdeInteractiveComponentType componentType = ParseInteractiveComponentType(descriptor?.ComponentTypeName);
            string componentTypeLabel = string.IsNullOrWhiteSpace(descriptor?.ComponentTypeName)
                ? "Control"
                : descriptor.ComponentTypeName;
            string? dataStructureName = descriptor?.DataStructureName;
            string? businessViewName = GetAttributeValue(descriptor?.Attributes, "BusinessViewName");
            string? dataItem = GetAttributeValue(descriptor?.Attributes, "DataItem");
            string? tableName = GetAttributeValue(descriptor?.Attributes, "Table");
            bool? isVisible = TryGetBooleanAttribute(descriptor?.Attributes, "Visible");

            JdeBusinessViewInfo? businessView = LoadInteractiveBusinessViewInfo(
                businessViewName,
                businessViewCache,
                businessViewService);

            IReadOnlyList<JdeInteractiveEventSpec> events = BuildInteractiveEventSpecs(
                rowsByControl.TryGetValue(controlId, out IReadOnlyList<EventRulesLinkRow>? rows)
                    ? rows
                    : Array.Empty<EventRulesLinkRow>(),
                ProductTypeFda,
                formDisplayName,
                name,
                dataStructureName);

            var component = new JdeInteractiveComponentSpec
            {
                ControlId = controlId,
                ObjectId = TryGetIntAttribute(descriptor?.Attributes, "ObjectId"),
                Name = name,
                ComponentType = componentType,
                ComponentTypeLabel = componentTypeLabel,
                ParentControlId = descriptor?.ParentControlId,
                DisplayOrder = descriptor?.DisplayOrder,
                DataStructureName = dataStructureName,
                BusinessViewName = businessViewName,
                DataItem = dataItem,
                TableName = tableName,
                IsVisible = isVisible,
                Events = events,
                Attributes = descriptor == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : ToAttributeDictionary(descriptor.Attributes)
            };

            component.MetadataSections = BuildInteractiveComponentMetadataSections(component, descriptor, businessView);
            componentsById[controlId] = component;
            parentById[controlId] = descriptor?.ParentControlId;
        }

        foreach (KeyValuePair<int, int?> entry in parentById.Where(entry => entry.Value.HasValue))
        {
            if (!componentsById.TryGetValue(entry.Key, out JdeInteractiveComponentSpec? child) ||
                !componentsById.TryGetValue(entry.Value!.Value, out JdeInteractiveComponentSpec? parent))
            {
                continue;
            }

            parent.Children = parent.Children
                .Concat(new[] { child })
                .OrderBy(component => component.DisplayOrder ?? int.MaxValue)
                .ThenBy(component => component.ControlId)
                .ToList();
        }

        return componentsById.Values
            .Where(component => !component.ParentControlId.HasValue || !componentsById.ContainsKey(component.ParentControlId.Value))
            .OrderBy(component => component.DisplayOrder ?? int.MaxValue)
            .ThenBy(component => component.ControlId)
            .ToList();
    }

    private InteractiveApplicationCatalog LoadInteractiveApplicationCatalog(string objectName)
    {
        var forms = new Dictionary<string, ApplicationFormDescriptor>(StringComparer.OrdinalIgnoreCase);
        var controls = new Dictionary<EventRulesControlKey, ApplicationControlDescriptor>();
        var formDescriptions = LoadApplicationFormDescriptions(objectName);
        ApplicationDescriptor applicationDescriptor = ApplicationDescriptor.Empty;

        TableLayout? layout = _options.UseRowLayoutTables
            ? TableLayoutLoader.Load(F98751Structures.TableName)
            : null;

        if (!TryOpenSpecHandle(JdeSpecFileType.FdaSpec, F98751Structures.TableName, fallbackTableName: null, out IntPtr hSpec))
        {
            return new InteractiveApplicationCatalog(applicationDescriptor, forms, controls, formDescriptions);
        }

        IntPtr hConvert = IntPtr.Zero;
        try
        {
            int convertInit = JdeSpecEncapApi.jdeSpecInitXMLConvertHandle(out hConvert, JdeSpecFileType.FdaSpec);
            if (convertInit != JDESPEC_SUCCESS || hConvert == IntPtr.Zero)
            {
                return new InteractiveApplicationCatalog(applicationDescriptor, forms, controls, formDescriptions);
            }

            TrySelectSpecByObjectName(hSpec, objectName, "[APPLSPEC]");

            while (true)
            {
                var specData = new JdeSpecData();
                int fetchResult = JdeSpecEncapApi.jdeSpecFetch(hSpec, ref specData);
                if (fetchResult != JDESPEC_SUCCESS)
                {
                    break;
                }

                try
                {
                    string rowObjectName = ReadSpecRecordString(
                        layout,
                        specData.RdbRecord,
                        F98751Structures.Columns.ObjectName);
                    if (!string.IsNullOrWhiteSpace(rowObjectName) &&
                        !string.Equals(rowObjectName, objectName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string rowFormName = ReadSpecRecordString(
                        layout,
                        specData.RdbRecord,
                        F98751Structures.Columns.FormName);
                    int rowControlId = ReadSpecRecordInt32(
                        layout,
                        specData.RdbRecord,
                        F98751Structures.Columns.ControlId);

                    string? xml = TryConvertSpecDataToXml(hConvert, ref specData);
                    if (string.IsNullOrWhiteSpace(xml) ||
                        !TryGetFirstApplicationComponentElement(xml, out XElement componentElement))
                    {
                        continue;
                    }

                    ReadApplicationRecordContext(
                        layout,
                        specData,
                        xml,
                        out int rowEventId,
                        out int eventId3,
                        out int rowRecordType);

                    string localName = componentElement.Name.LocalName;
                    if (string.Equals(localName, "FDAApplication", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseApplicationDescriptor(componentElement, out ApplicationDescriptor descriptor))
                        {
                            continue;
                        }

                        int score = GetApplicationDescriptorScore(rowEventId, eventId3, descriptor);
                        if (score > applicationDescriptor.Score)
                        {
                            applicationDescriptor = descriptor with { Score = score };
                        }

                        continue;
                    }

                    if (string.Equals(localName, "FDAForm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(localName, "FDASubForm", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseApplicationFormDescriptor(componentElement, out ApplicationFormDescriptor descriptor))
                        {
                            continue;
                        }

                        string formName = NormalizeFormKey(descriptor.FormName);
                        if (string.IsNullOrWhiteSpace(formName))
                        {
                            continue;
                        }

                        int score = GetApplicationFormDescriptorScore(rowEventId, eventId3, descriptor);
                        if (!forms.TryGetValue(formName, out ApplicationFormDescriptor? existing) ||
                            score > existing.Score)
                        {
                            forms[formName] = descriptor with
                            {
                                FormName = formName,
                                Score = score
                            };
                        }

                        continue;
                    }

                    if (!IsApplicationMetadataRecordType(rowRecordType) ||
                        !TryParseApplicationControlDescriptor(xml, rowRecordType, out ApplicationControlDescriptor controlDescriptor) ||
                        !TryResolveApplicationActualControlKeyFromElement(
                            rowFormName,
                            rowControlId,
                            componentElement,
                            out EventRulesControlKey controlKey))
                    {
                        continue;
                    }

                    int controlScore = GetApplicationControlDescriptorScore(rowEventId, eventId3, controlDescriptor);
                    if (!controls.TryGetValue(controlKey, out ApplicationControlDescriptor? existingControl) ||
                        controlScore > existingControl.Score)
                    {
                        controls[controlKey] = controlDescriptor with { Score = controlScore };
                    }
                }
                finally
                {
                    JdeSpecEncapApi.jdeSpecFreeData(ref specData);
                }
            }
        }
        finally
        {
            if (hConvert != IntPtr.Zero)
            {
                JdeSpecEncapApi.jdeSpecClose(hConvert);
            }

            JdeSpecEncapApi.jdeSpecClose(hSpec);
        }

        return new InteractiveApplicationCatalog(applicationDescriptor, forms, controls, formDescriptions);
    }

    private JdeInteractiveDataStructureSpec? LoadInteractiveDataStructureSpec(
        string? templateName,
        string defaultType,
        IDictionary<string, JdeInteractiveDataStructureSpec?> cache)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        string normalizedTemplateName = templateName.Trim();
        if (cache.TryGetValue(normalizedTemplateName, out JdeInteractiveDataStructureSpec? cached))
        {
            return cached;
        }

        IReadOnlyList<JdeSpecXmlDocument> documents = GetDataStructureXmlDocuments(normalizedTemplateName);
        JdeInteractiveDataStructureSpec? spec = null;
        foreach (JdeSpecXmlDocument document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.Xml))
            {
                continue;
            }

            try
            {
                DataStructureTemplate template = DataStructureTemplate.Parse(normalizedTemplateName, document.Xml);
                spec = new JdeInteractiveDataStructureSpec
                {
                    Name = normalizedTemplateName,
                    Type = string.IsNullOrWhiteSpace(defaultType) ? "Data Structure" : defaultType,
                    Description = template.Description,
                    Items = template.ItemsById.Values
                        .Select(item => new JdeInteractiveDataStructureItemSpec
                        {
                            Sequence = ParseDisplaySequence(item.DisplaySequence),
                            Name = item.FieldName,
                            DataItem = item.Alias
                        })
                        .OrderBy(item => item.Sequence)
                        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
                break;
            }
            catch
            {
                // Ignore malformed payloads and continue to the next available XML document.
            }
        }

        cache[normalizedTemplateName] = spec;
        return spec;
    }

    private static JdeBusinessViewInfo? LoadInteractiveBusinessViewInfo(
        string? viewName,
        IDictionary<string, JdeBusinessViewInfo?> cache,
        SpecBusinessViewMetadataService businessViewService)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return null;
        }

        string normalizedViewName = viewName.Trim();
        if (cache.TryGetValue(normalizedViewName, out JdeBusinessViewInfo? cached))
        {
            return cached;
        }

        JdeBusinessViewInfo? info = businessViewService.GetBusinessViewInfo(normalizedViewName);
        cache[normalizedViewName] = info;
        return info;
    }

    private IReadOnlyList<JdeInteractiveEventSpec> BuildInteractiveEventSpecs(
        IReadOnlyList<EventRulesLinkRow> rows,
        int productType,
        string formDisplayName,
        string? componentName,
        string? templateName)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<JdeInteractiveEventSpec>();
        }

        var events = new List<JdeInteractiveEventSpec>();
        foreach (var eventGroup in rows
                     .GroupBy(row => new { row.EventSpecKey, row.EventId, row.EventOrder, row.Id3 })
                     .OrderBy(group => group.Key.EventOrder)
                     .ThenBy(group => group.Key.EventId, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(group => group.Key.Id3))
        {
            var first = eventGroup.First();
            string name = FormatEventLabel(
                eventGroup.Key.EventId,
                eventGroup.Key.Id3,
                productType,
                first.FormName);

            var eventSpec = new JdeInteractiveEventSpec
            {
                Name = name,
                EventSpecKey = eventGroup.Key.EventSpecKey,
                EventId = ToOptional(eventGroup.Key.EventId),
                EventId3 = eventGroup.Key.Id3 == 0 ? null : eventGroup.Key.Id3,
                TemplateName = templateName
            };
            eventSpec.MetadataSections = BuildInteractiveEventMetadataSections(
                eventSpec,
                formDisplayName,
                componentName,
                ToOptional(first.Version));
            events.Add(eventSpec);
        }

        return events;
    }

    private IReadOnlyList<JdeSpecMetadataSection> BuildInteractiveApplicationMetadataSections(
        string objectName,
        string applicationName,
        ApplicationDescriptor descriptor,
        JdeInteractiveDataStructureSpec? processingOptions)
    {
        var sections = new List<JdeSpecMetadataSection>();
        var properties = new List<JdeSpecMetadataItem?>
        {
            CreateMetadataItem("Object", objectName),
            CreateMetadataItem("Name", applicationName),
            CreateMetadataItem("Processing Option Template", descriptor.ProcessingOptionTemplate),
            CreateMetadataItem("Text ID", descriptor.TextId)
        };
        AddMetadataSection(sections, "Properties", properties);

        if (processingOptions != null)
        {
            AddMetadataSection(
                sections,
                "Processing Options",
                new[]
                {
                    CreateMetadataItem("Name", processingOptions.Name),
                    CreateMetadataItem("Type", processingOptions.Type),
                    CreateMetadataItem("Description", processingOptions.Description),
                    CreateMetadataItem("Item Count", processingOptions.Items.Count)
                });

            if (processingOptions.Items.Count > 0)
            {
                AddMetadataSection(
                    sections,
                    "Processing Option Items",
                    processingOptions.Items.Select(item =>
                        CreateMetadataItem(
                            item.Sequence.ToString(CultureInfo.InvariantCulture),
                            $"{item.Name} [{item.DataItem}]")));
            }
        }

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TextId",
            "ProcessingOptionTemplate"
        };
        AddAttributeSection(sections, "Attributes", descriptor.Attributes, consumed, _ => true);
        return sections;
    }

    private IReadOnlyList<JdeSpecMetadataSection> BuildInteractiveFormMetadataSections(
        JdeInteractiveFormSpec form,
        ApplicationFormDescriptor? descriptor)
    {
        var sections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            sections,
            "Properties",
            new[]
            {
                CreateMetadataItem("Form", form.ObjectName),
                CreateMetadataItem("Name", form.Name),
                CreateMetadataItem("Type", form.FormTypeLabel),
                CreateMetadataItem("Business View", form.BusinessViewName),
                CreateMetadataItem("Data Structure", form.DataStructureName),
                CreateMetadataItem("Control Count", form.ControlCount),
                CreateMetadataItem("Fetch On Form Business View", form.FetchOnFormBusinessView),
                CreateMetadataItem("Update On Form Business View", form.UpdateOnFormBusinessView),
                CreateMetadataItem("Fetch On Grid Business View", form.FetchOnGridBusinessView),
                CreateMetadataItem("Update On Grid Business View", form.UpdateOnGridBusinessView),
                CreateMetadataItem("Transaction Type", form.TransactionType),
                CreateMetadataItem("Text ID", descriptor?.TextId)
            });

        if (form.DataStructure != null)
        {
            sections.AddRange(BuildInteractiveDataStructureMetadataSections(form.DataStructure));
        }

        if (form.BusinessView != null)
        {
            sections.AddRange(BuildInteractiveBusinessViewMetadataSections(form.BusinessView));
        }

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FormName",
            "FormType",
            "FormTitleId",
            "TextId",
            "BusinessViewName",
            "DSTemplateName",
            "StateFlags"
        };
        AddAttributeSection(sections, "Attributes", descriptor?.Attributes, consumed, _ => true);
        return sections;
    }

    private IReadOnlyList<JdeSpecMetadataSection> BuildInteractiveComponentMetadataSections(
        JdeInteractiveComponentSpec component,
        ApplicationControlDescriptor? descriptor,
        JdeBusinessViewInfo? businessView)
    {
        var sections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            sections,
            "Properties",
            new[]
            {
                CreateMetadataItem("Control ID", component.ControlId),
                CreateMetadataItem("Object ID", component.ObjectId),
                CreateMetadataItem("Name", component.Name),
                CreateMetadataItem("Type", component.ComponentTypeLabel),
                CreateMetadataItem("Parent Control ID", component.ParentControlId),
                CreateMetadataItem("Display Order", component.DisplayOrder),
                CreateMetadataItem("Data Item", component.DataItem),
                CreateMetadataItem("Table", component.TableName),
                CreateMetadataItem("Business View", component.BusinessViewName),
                CreateMetadataItem("Data Structure", component.DataStructureName),
                CreateMetadataItem("Visible", component.IsVisible),
                CreateMetadataItem("Text ID", descriptor?.TextId)
            });

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ObjectId",
            "ObjectID",
            "ControlId",
            "ControlID",
            "CtrlId",
            "GridID",
            "GridId",
            "TabOrderIndex",
            "SequenceNumber",
            "BusinessViewName",
            "DataItem",
            "Table",
            "Visible",
            "TextId",
            "FDATextId",
            "ColumnTitleId",
            "ColumnTitleID",
            "PromptTextId",
            "TitleTextId",
            "DataStructureTemplate",
            "DSTemplateName",
            "TemplateName",
            "ProcessingOptionTemplate",
            "FormInterconnectTemplate"
        };

        var generalAttributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TabStop",
            "Group",
            "ReadOnly",
            "Disabled",
            "Sort",
            "LeftCoordinate",
            "TopCoordinate",
            "Width",
            "Height",
            "InputCapable",
            "OverrideText",
            "OverrideTextFlag",
            "NumberOfTextChar",
            "WinStyleFlag",
            "StandardFlags",
            "CGStyle",
            "Flags"
        };
        AddAttributeSection(sections, "General Attributes", descriptor?.Attributes, consumed, key => generalAttributeNames.Contains(key));

        string typeSpecificTitle = component.ComponentType switch
        {
            JdeInteractiveComponentType.PushButton => "Push Button Properties",
            JdeInteractiveComponentType.Grid => "Options",
            JdeInteractiveComponentType.GridColumn => "Column Properties",
            _ => "Attributes"
        };
        AddAttributeSection(sections, typeSpecificTitle, descriptor?.Attributes, consumed, key => component.ComponentType switch
        {
            JdeInteractiveComponentType.PushButton => key.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                                                     key.Contains("Push", StringComparison.OrdinalIgnoreCase),
            JdeInteractiveComponentType.Grid => key.Contains("Grid", StringComparison.OrdinalIgnoreCase) ||
                                               key.Contains("RowCount", StringComparison.OrdinalIgnoreCase),
            JdeInteractiveComponentType.GridColumn => key.Contains("Column", StringComparison.OrdinalIgnoreCase) ||
                                                     key.Contains("Sort", StringComparison.OrdinalIgnoreCase),
            _ => false
        });

        AddAttributeSection(sections, "Attributes", descriptor?.Attributes, consumed, _ => true);

        if (businessView != null)
        {
            sections.AddRange(BuildInteractiveBusinessViewMetadataSections(businessView));
        }

        return sections;
    }

    private IReadOnlyList<JdeSpecMetadataSection> BuildInteractiveEventMetadataSections(
        JdeInteractiveEventSpec eventSpec,
        string formDisplayName,
        string? componentName,
        string? versionName)
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
                CreateMetadataItem("Event ID3", eventSpec.EventId3),
                CreateMetadataItem("Form", formDisplayName),
                CreateMetadataItem("Component", componentName),
                CreateMetadataItem("Version", versionName),
                CreateMetadataItem("Data Structure", eventSpec.TemplateName)
            });
        return sections;
    }

    private static IReadOnlyList<JdeSpecMetadataSection> BuildInteractiveDataStructureMetadataSections(
        JdeInteractiveDataStructureSpec dataStructure)
    {
        var sections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            sections,
            "Data Structure",
            new[]
            {
                CreateMetadataItem("Name", dataStructure.Name),
                CreateMetadataItem("Type", dataStructure.Type),
                CreateMetadataItem("Description", dataStructure.Description),
                CreateMetadataItem("Item Count", dataStructure.Items.Count)
            });

        if (dataStructure.Items.Count > 0)
        {
            AddMetadataSection(
                sections,
                "Data Structure Items",
                dataStructure.Items.Select(item =>
                    CreateMetadataItem(
                        item.Sequence.ToString(CultureInfo.InvariantCulture),
                        $"{item.Name} [{item.DataItem}]")));
        }

        return sections;
    }

    private static IReadOnlyList<JdeSpecMetadataSection> BuildInteractiveBusinessViewMetadataSections(
        JdeBusinessViewInfo businessView)
    {
        var sections = new List<JdeSpecMetadataSection>();
        AddMetadataSection(
            sections,
            "Business View",
            new[]
            {
                CreateMetadataItem("Name", businessView.ViewName),
                CreateMetadataItem("Description", businessView.Description),
                CreateMetadataItem("System Code", businessView.SystemCode),
                CreateMetadataItem("Table Count", businessView.Tables.Count),
                CreateMetadataItem("Column Count", businessView.Columns.Count),
                CreateMetadataItem("Join Count", businessView.Joins.Count)
            });

        if (businessView.Tables.Count > 0)
        {
            AddMetadataSection(
                sections,
                "Tables",
                businessView.Tables.Select((table, index) =>
                    CreateMetadataItem(
                        $"Table {index + 1}",
                        $"{table.TableName} | Instances={table.InstanceCount} | Primary Index={table.PrimaryIndexId}")));
        }

        if (businessView.Columns.Count > 0)
        {
            AddMetadataSection(
                sections,
                "Columns",
                businessView.Columns
                    .OrderBy(column => column.Sequence)
                    .Select(column =>
                        CreateMetadataItem(
                            column.Sequence.ToString(CultureInfo.InvariantCulture),
                            $"{column.DataItem} | {column.TableName}({column.InstanceId}) | Length={column.Length} | Decimals={column.Decimals} | Type={column.TypeCode}")));
        }

        if (businessView.Joins.Count > 0)
        {
            AddMetadataSection(
                sections,
                "Joins",
                businessView.Joins.Select((join, index) =>
                    CreateMetadataItem(
                        $"Join {index + 1}",
                        $"{join.PrimaryTable}.{join.PrimaryColumn} {join.JoinOperator} {join.ForeignTable}.{join.ForeignColumn} ({join.JoinType})")));
        }

        return sections;
    }

    private static string ResolveApplicationName(
        ApplicationDescriptor descriptor,
        string objectName,
        IReadOnlyDictionary<int, string> textById)
    {
        if (descriptor.TextId.HasValue &&
            textById.TryGetValue(descriptor.TextId.Value, out string? text) &&
            !string.IsNullOrWhiteSpace(text))
        {
            return NormalizeSingleLineLabel(text);
        }

        return objectName;
    }

    private static string ResolveFormDisplayName(
        string formName,
        ApplicationFormDescriptor? descriptor,
        IReadOnlyDictionary<string, string> formDescriptions,
        IReadOnlyDictionary<int, string> textById)
    {
        if (descriptor?.TextId is int textId &&
            textById.TryGetValue(textId, out string? text) &&
            !string.IsNullOrWhiteSpace(text))
        {
            return NormalizeSingleLineLabel(text);
        }

        if (formDescriptions.TryGetValue(formName, out string? description) &&
            !string.IsNullOrWhiteSpace(description))
        {
            return NormalizeSingleLineLabel(description);
        }

        string alternateFormName = GetAlternateApplicationFormName(formName);
        if (!string.IsNullOrWhiteSpace(alternateFormName) &&
            formDescriptions.TryGetValue(alternateFormName, out description) &&
            !string.IsNullOrWhiteSpace(description))
        {
            return NormalizeSingleLineLabel(description);
        }

        return formName;
    }

    private static int ParseDisplaySequence(string? displaySequence)
    {
        return int.TryParse(displaySequence, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    private static int GetApplicationDescriptorScore(int eventId, int eventId3, ApplicationDescriptor descriptor)
    {
        int score = 0;
        if (eventId == 0)
        {
            score += 30;
        }

        if (eventId3 == 0)
        {
            score += 10;
        }

        if (descriptor.TextId.HasValue)
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(descriptor.ProcessingOptionTemplate))
        {
            score += 8;
        }

        score += Math.Min(descriptor.Attributes.Count, 6);
        return score;
    }

    private static int GetApplicationFormDescriptorScore(int eventId, int eventId3, ApplicationFormDescriptor descriptor)
    {
        int score = 0;
        if (eventId == 0)
        {
            score += 30;
        }

        if (eventId3 == 0)
        {
            score += 10;
        }

        if (descriptor.TextId.HasValue)
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(descriptor.DataStructureName))
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(descriptor.BusinessViewName))
        {
            score += 8;
        }

        if (descriptor.FormType != JdeInteractiveFormType.Unknown)
        {
            score += 4;
        }

        score += Math.Min(descriptor.Attributes.Count, 6);
        return score;
    }

    private static bool TryParseApplicationDescriptor(XElement componentElement, out ApplicationDescriptor descriptor)
    {
        descriptor = ApplicationDescriptor.Empty;
        if (!string.Equals(componentElement.Name.LocalName, "FDAApplication", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        descriptor = new ApplicationDescriptor
        {
            TextId = TryReadFirstAttributeAsInt(componentElement, "TextId"),
            ProcessingOptionTemplate = ReadFirstNonEmptyAttribute(componentElement, "ProcessingOptionTemplate"),
            Attributes = componentElement.Attributes()
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Value))
                .Select(attribute => new KeyValuePair<string, string>(
                    attribute.Name.LocalName,
                    NormalizeText(attribute.Value)))
                .ToList(),
            Score = 0
        };

        return true;
    }

    private static bool TryParseApplicationFormDescriptor(XElement componentElement, out ApplicationFormDescriptor descriptor)
    {
        descriptor = ApplicationFormDescriptor.Empty;

        string localName = componentElement.Name.LocalName;
        bool isForm = string.Equals(localName, "FDAForm", StringComparison.OrdinalIgnoreCase);
        bool isSubform = string.Equals(localName, "FDASubForm", StringComparison.OrdinalIgnoreCase);
        if (!isForm && !isSubform)
        {
            return false;
        }

        string? formName = ReadFirstNonEmptyAttribute(componentElement, "FormName");
        if (string.IsNullOrWhiteSpace(formName))
        {
            return false;
        }

        var attributes = componentElement.Attributes()
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Value))
            .Select(attribute => new KeyValuePair<string, string>(
                attribute.Name.LocalName,
                NormalizeText(attribute.Value)))
            .ToList();

        int stateFlags = TryReadFirstAttributeAsInt(componentElement, "StateFlags") ?? 0;
        bool? updateOnFormBusinessView = TryGetBooleanAttribute(attributes, "UpdateOnFormSubFormBusinessView");
        bool? fetchOnFormBusinessView = TryGetBooleanAttribute(attributes, "FetchOnFormSubFormBusinessView");
        bool? updateOnGridBusinessView = TryGetBooleanAttribute(attributes, "UpdateOnGridBusinessView");
        bool? fetchOnGridBusinessView = TryGetBooleanAttribute(attributes, "FetchOnGridBusinessView");

        if (!updateOnFormBusinessView.HasValue && stateFlags != 0)
        {
            updateOnFormBusinessView = (stateFlags & 0x02) != 0;
        }

        if (!fetchOnFormBusinessView.HasValue && stateFlags != 0)
        {
            fetchOnFormBusinessView = (stateFlags & 0x04) != 0;
        }

        string? transactionType = ReadFirstNonEmptyAttribute(componentElement, "TransactionType");
        if (string.IsNullOrWhiteSpace(transactionType) && stateFlags != 0 && (stateFlags & 0x20) != 0)
        {
            transactionType = "Transaction Disabled";
        }

        string? formTypeToken = isSubform
            ? "Subform"
            : ReadFirstNonEmptyAttribute(componentElement, "FormType");

        descriptor = new ApplicationFormDescriptor
        {
            FormName = NormalizeFormKey(formName),
            TextId = TryReadFirstAttributeAsInt(componentElement, "FormTitleId", "TextId"),
            FormType = isSubform ? JdeInteractiveFormType.Subform : JdeInteractiveFormType.Form,
            FormTypeLabel = HumanizeFormType(formTypeToken),
            BusinessViewName = ReadFirstNonEmptyAttribute(componentElement, "BusinessViewName", "ViewName"),
            DataStructureName = ReadFirstNonEmptyAttribute(
                componentElement,
                "DSTemplateName",
                "DataStructureTemplate",
                "FormInterconnectTemplate"),
            UpdateOnFormBusinessView = updateOnFormBusinessView,
            FetchOnFormBusinessView = fetchOnFormBusinessView,
            UpdateOnGridBusinessView = updateOnGridBusinessView,
            FetchOnGridBusinessView = fetchOnGridBusinessView,
            TransactionType = transactionType,
            Attributes = attributes,
            Score = 0
        };

        return true;
    }

    private static bool TryResolveApplicationActualControlKeyFromElement(
        string rowFormName,
        int rowControlId,
        XElement componentElement,
        out EventRulesControlKey controlKey)
    {
        controlKey = default;

        string? formName = ReadFirstNonEmptyAttribute(componentElement, "FormName");
        if (string.IsNullOrWhiteSpace(formName))
        {
            formName = rowFormName;
        }

        string normalizedFormName = NormalizeFormKey(formName);
        if (string.IsNullOrWhiteSpace(normalizedFormName))
        {
            return false;
        }

        int? controlId = TryReadFirstAttributeAsInt(
            componentElement,
            "ControlId",
            "ControlID",
            "CtrlId",
            "ObjectId",
            "ObjectID",
            "TabControlObjectId",
            "ChildObjectId",
            "WindowItemId",
            "HyperControlObjectId",
            "TextBlockControlObjectId");
        if (!controlId.HasValue)
        {
            controlId = rowControlId > 0 ? rowControlId : null;
        }

        if (!controlId.HasValue || controlId.Value < 0)
        {
            return false;
        }

        controlKey = new EventRulesControlKey(normalizedFormName, controlId.Value);
        return true;
    }

    private static JdeInteractiveComponentType ParseInteractiveComponentType(string? componentTypeName)
    {
        if (string.IsNullOrWhiteSpace(componentTypeName))
        {
            return JdeInteractiveComponentType.Unknown;
        }

        return componentTypeName.Trim() switch
        {
            "Push Button" => JdeInteractiveComponentType.PushButton,
            "Grid" => JdeInteractiveComponentType.Grid,
            "Grid Column" => JdeInteractiveComponentType.GridColumn,
            "Text Block" => JdeInteractiveComponentType.TextBlock,
            "Function" => JdeInteractiveComponentType.Function,
            "Tab Control" => JdeInteractiveComponentType.TabControl,
            "Page" => JdeInteractiveComponentType.Page,
            "Check Box" => JdeInteractiveComponentType.CheckBox,
            "Radio Button" => JdeInteractiveComponentType.RadioButton,
            "Combo Box" => JdeInteractiveComponentType.ComboBox,
            "List Box" => JdeInteractiveComponentType.ListBox,
            "Control" => JdeInteractiveComponentType.Control,
            _ => JdeInteractiveComponentType.Unknown
        };
    }

    private static string HumanizeFormType(string? formType)
    {
        if (string.IsNullOrWhiteSpace(formType))
        {
            return "Unknown";
        }

        string normalized = formType.Trim().Replace('_', ' ');
        var parts = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant());
        return string.Join(" ", parts);
    }

    private static string NormalizePushButtonBehavior(string? behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior))
        {
            return string.Empty;
        }

        string normalized = behavior.Trim();
        if (normalized.StartsWith("PUSHB_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[6..];
        }

        return HumanizeFormType(normalized);
    }

    private static IReadOnlyDictionary<string, string> ToAttributeDictionary(
        IEnumerable<KeyValuePair<string, string>>? attributes)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (attributes == null)
        {
            return dictionary;
        }

        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.Key) ||
                string.IsNullOrWhiteSpace(attribute.Value) ||
                dictionary.ContainsKey(attribute.Key))
            {
                continue;
            }

            dictionary[attribute.Key] = attribute.Value.Trim();
        }

        return dictionary;
    }

    private static string? GetAttributeValue(IEnumerable<KeyValuePair<string, string>>? attributes, string attributeName)
    {
        if (attributes == null || string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            if (string.Equals(attribute.Key, attributeName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(attribute.Value))
            {
                return attribute.Value.Trim();
            }
        }

        return null;
    }

    private IReadOnlyDictionary<string, string> LoadApplicationDataDictionaryTitles(
        IEnumerable<ApplicationControlDescriptor> descriptors,
        IReadOnlyDictionary<int, string> textById)
    {
        var unresolvedDataItems = descriptors
            .Where(descriptor => !descriptor.TextId.HasValue || !textById.ContainsKey(descriptor.TextId.Value))
            .Select(descriptor => GetAttributeValue(descriptor.Attributes, "DataItem"))
            .Where(dataItem => !string.IsNullOrWhiteSpace(dataItem))
            .Select(dataItem => dataItem!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return LoadDataDictionaryTitles(unresolvedDataItems);
    }

    private IReadOnlyDictionary<string, string> LoadDataDictionaryTitles(IEnumerable<string> dataItems)
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var items = dataItems
            .Where(dataItem => !string.IsNullOrWhiteSpace(dataItem))
            .Select(dataItem => dataItem.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (items.Count == 0)
        {
            return titles;
        }

        IntPtr dictionary = JdeSpecApi.jdeOpenDictionaryX(_hUser);
        if (dictionary == IntPtr.Zero)
        {
            return titles;
        }

        try
        {
            foreach (string dataItem in items)
            {
                string? title = LoadDataDictionaryTitle(dictionary, dataItem);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    titles[dataItem] = title;
                }
            }
        }
        finally
        {
            JdeSpecApi.jdeCloseDictionary(dictionary);
        }

        return titles;
    }

    private static string? LoadDataDictionaryTitle(IntPtr dictionary, string dataItem)
    {
        string? columnTitle = TryReadDataDictionaryText(dictionary, dataItem, DDT_COL_TITLE);
        if (!string.IsNullOrWhiteSpace(columnTitle))
        {
            return NormalizeSingleLineLabel(columnTitle);
        }

        string? alphaDescription = TryReadDataDictionaryText(dictionary, dataItem, DDT_ALPHA_DESC);
        if (!string.IsNullOrWhiteSpace(alphaDescription))
        {
            return NormalizeSingleLineLabel(alphaDescription);
        }

        string? rowDescription = TryReadDataDictionaryText(dictionary, dataItem, DDT_ROW_DESC);
        return string.IsNullOrWhiteSpace(rowDescription)
            ? null
            : NormalizeSingleLineLabel(rowDescription);
    }

    private static string? TryReadDataDictionaryText(IntPtr dictionary, string dataItem, int textType)
    {
        IntPtr textPtr = IntPtr.Zero;
        try
        {
            textPtr = JdeSpecApi.jdeAllocFetchDDTextFromDDItemNameOvr(
                dictionary,
                dataItem,
                textType,
                "  ",
                null);
            if (textPtr == IntPtr.Zero)
            {
                return null;
            }

            var ddText = Marshal.PtrToStructure<DDTEXT>(textPtr);
            int textOffset = Marshal.OffsetOf<DDTEXT>(nameof(DDTEXT.szText)).ToInt32();
            int bytesAvailable = Math.Max(0, (int)ddText.lVarLen - textOffset);
            int charCount = bytesAvailable / 2;
            string? text = charCount > 0
                ? Marshal.PtrToStringUni(IntPtr.Add(textPtr, textOffset), charCount)
                : null;
            return NormalizeText(text ?? string.Empty);
        }
        finally
        {
            if (textPtr != IntPtr.Zero)
            {
                JdeSpecApi.jdeTextFree(textPtr);
            }
        }
    }

    private static string? ResolveApplicationControlDisplayText(
        int? textId,
        IEnumerable<KeyValuePair<string, string>>? attributes,
        IReadOnlyDictionary<int, string> textById,
        IReadOnlyDictionary<string, string> dataDictionaryTitles)
    {
        if (textId.HasValue &&
            textId.Value > 0 &&
            textById.TryGetValue(textId.Value, out string? resolvedText) &&
            !string.IsNullOrWhiteSpace(resolvedText))
        {
            return resolvedText;
        }

        string? dataItem = GetAttributeValue(attributes, "DataItem");
        if (string.IsNullOrWhiteSpace(dataItem))
        {
            return null;
        }

        if (dataDictionaryTitles.TryGetValue(dataItem, out string? title) &&
            !string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        return dataItem;
    }

    private static int? TryGetIntAttribute(IEnumerable<KeyValuePair<string, string>>? attributes, string attributeName)
    {
        string? value = GetAttributeValue(attributes, attributeName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    private static bool? TryGetBooleanAttribute(IEnumerable<KeyValuePair<string, string>>? attributes, string attributeName)
    {
        string? value = GetAttributeValue(attributes, attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "1" => true,
            "0" => false,
            "Y" => true,
            "N" => false,
            "YES" => true,
            "NO" => false,
            "TRUE" => true,
            "FALSE" => false,
            _ => bool.TryParse(value, out bool parsed) ? parsed : null
        };
    }

    private static string? FormatBoolean(bool? value)
    {
        return value.HasValue
            ? (value.Value ? "True" : "False")
            : null;
    }

    private static JdeSpecMetadataItem? CreateMetadataItem(string label, object? value)
    {
        if (value == null)
        {
            return null;
        }

        string? text = value switch
        {
            string stringValue => string.IsNullOrWhiteSpace(stringValue) ? null : stringValue.Trim(),
            bool booleanValue => FormatBoolean(booleanValue),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            _ when value is IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new JdeSpecMetadataItem
        {
            Label = label,
            Value = text
        };
    }

    private static void AddMetadataSection(
        ICollection<JdeSpecMetadataSection> sections,
        string title,
        IEnumerable<JdeSpecMetadataItem?> items)
    {
        var materializedItems = items
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item!)
            .ToList();
        if (materializedItems.Count == 0)
        {
            return;
        }

        sections.Add(new JdeSpecMetadataSection
        {
            Title = title,
            Items = materializedItems
        });
    }

    private static void AddAttributeSection(
        ICollection<JdeSpecMetadataSection> sections,
        string title,
        IEnumerable<KeyValuePair<string, string>>? attributes,
        ISet<string> consumed,
        Func<string, bool> includeAttribute)
    {
        if (attributes == null)
        {
            return;
        }

        var items = new List<JdeSpecMetadataItem?>();
        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.Key) ||
                string.IsNullOrWhiteSpace(attribute.Value) ||
                consumed.Contains(attribute.Key) ||
                !includeAttribute(attribute.Key))
            {
                continue;
            }

            items.Add(CreateMetadataItem(HumanizeMetadataLabel(attribute.Key), attribute.Value));
            consumed.Add(attribute.Key);
        }

        AddMetadataSection(sections, title, items);
    }

    private static string HumanizeMetadataLabel(string value)
    {
        return value switch
        {
            "CGStyle" => "CG Style",
            "CtrlId" => "Control ID",
            "DataItem" => "Data Item",
            "DSTemplateName" => "Data Structure Template",
            "FDATextId" => "Text ID",
            "FormTitleId" => "Form Title ID",
            "GridID" => "Grid ID",
            "HTMLGridRowCount" => "HTML Grid Row Count",
            "ObjectId" or "ObjectID" => "Object ID",
            "TabOrderIndex" => "Tab Order",
            "TextId" => "Text ID",
            "WindowItemId" => "Window Item ID",
            _ => SplitPascalCase(value.Replace('_', ' ')).Replace(" Id", " ID", StringComparison.Ordinal)
        };
    }

    private static string BuildInteractiveFormSummary(JdeInteractiveFormSpec form)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(form.FormTypeLabel))
        {
            parts.Add($"Type={form.FormTypeLabel}");
        }

        if (!string.IsNullOrWhiteSpace(form.BusinessViewName))
        {
            parts.Add($"BusinessView={form.BusinessViewName}");
        }

        parts.Add($"Controls={form.ControlCount}");

        return parts.Count == 0
            ? string.Empty
            : $"Form: {string.Join(" | ", parts)}";
    }

    private static string BuildInteractiveComponentSummary(JdeInteractiveComponentSpec component)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(component.ComponentTypeLabel))
        {
            parts.Add($"Type={component.ComponentTypeLabel}");
        }

        if (!string.IsNullOrWhiteSpace(component.DataItem))
        {
            parts.Add($"DataItem={component.DataItem}");
        }

        if (!string.IsNullOrWhiteSpace(component.TableName))
        {
            parts.Add($"Table={component.TableName}");
        }

        if (component.ComponentType == JdeInteractiveComponentType.PushButton)
        {
            string behavior = NormalizePushButtonBehavior(GetAttributeValue(component.Attributes, "PushButtonAutoBehavior"));
            if (!string.IsNullOrWhiteSpace(behavior))
            {
                parts.Add($"Behavior={behavior}");
            }
        }

        return parts.Count == 0
            ? string.Empty
            : $"Component: {string.Join(" | ", parts)}";
    }

    private static string BuildInteractiveEventSummary(JdeInteractiveEventSpec eventSpec)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(eventSpec.EventId))
        {
            parts.Add($"Event ID={eventSpec.EventId}");
        }

        if (eventSpec.EventId3.HasValue)
        {
            parts.Add($"ID3={eventSpec.EventId3.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(eventSpec.TemplateName))
        {
            parts.Add($"Template={eventSpec.TemplateName}");
        }

        return parts.Count == 0
            ? string.Empty
            : $"Event: {string.Join(" | ", parts)}";
    }

    private sealed record InteractiveApplicationCatalog(
        ApplicationDescriptor ApplicationDescriptor,
        IReadOnlyDictionary<string, ApplicationFormDescriptor> Forms,
        IReadOnlyDictionary<EventRulesControlKey, ApplicationControlDescriptor> Controls,
        IReadOnlyDictionary<string, string> FormDescriptions);

    private sealed record ApplicationDescriptor
    {
        public static ApplicationDescriptor Empty { get; } = new();

        public int? TextId { get; init; }

        public string? ProcessingOptionTemplate { get; init; }

        public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; init; } =
            Array.Empty<KeyValuePair<string, string>>();

        public int Score { get; init; }
    }

    private sealed record ApplicationFormDescriptor
    {
        public static ApplicationFormDescriptor Empty { get; } = new();

        public string FormName { get; init; } = string.Empty;

        public int? TextId { get; init; }

        public JdeInteractiveFormType FormType { get; init; } = JdeInteractiveFormType.Unknown;

        public string FormTypeLabel { get; init; } = "Unknown";

        public string? BusinessViewName { get; init; }

        public string? DataStructureName { get; init; }

        public bool? UpdateOnFormBusinessView { get; init; }

        public bool? FetchOnFormBusinessView { get; init; }

        public bool? UpdateOnGridBusinessView { get; init; }

        public bool? FetchOnGridBusinessView { get; init; }

        public string? TransactionType { get; init; }

        public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; init; } =
            Array.Empty<KeyValuePair<string, string>>();

        public int Score { get; init; }
    }
}
