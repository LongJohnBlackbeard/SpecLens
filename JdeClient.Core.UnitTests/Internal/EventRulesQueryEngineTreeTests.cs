using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JdeClient.Core;
using JdeClient.Core.Models;

namespace JdeClient.Core.UnitTests.Internal;

public class EventRulesQueryEngineTreeTests
{
    [Test]
    public async Task BuildInteractiveApplicationTree_ProjectsMetadataAndKeepsEventRulesOnEventNodes()
    {
        Type engineType = typeof(JdeClient).Assembly.GetType("JdeClient.Core.Internal.EventRulesQueryEngine", throwOnError: true)!;
        object engine = RuntimeHelpers.GetUninitializedObject(engineType);
        MethodInfo buildTree = engineType.GetMethod(
            "BuildInteractiveApplicationTree",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var spec = new JdeInteractiveApplicationSpec
        {
            ObjectName = "P55OO",
            Name = "Order Overview",
            ProcessingOptionTemplateName = "T55OO",
            MetadataSections = new[]
            {
                new JdeSpecMetadataSection
                {
                    Title = "Properties",
                    Items = new[]
                    {
                        new JdeSpecMetadataItem { Label = "Object", Value = "P55OO" }
                    }
                }
            },
            Forms = new[]
            {
                new JdeInteractiveSubformSpec
                {
                    ObjectName = "S55OOA",
                    Name = "Routing",
                    FormType = JdeInteractiveFormType.Subform,
                    FormTypeLabel = "Subform",
                    DataStructureName = "S55OOA",
                    MetadataSections = new[]
                    {
                        new JdeSpecMetadataSection
                        {
                            Title = "Properties",
                            Items = new[]
                            {
                                new JdeSpecMetadataItem { Label = "Form", Value = "S55OOA" }
                            }
                        }
                    },
                    Events = new[]
                    {
                        new JdeInteractiveEventSpec
                        {
                            Name = "Form Variables [65535]",
                            EventSpecKey = "EV-FORM",
                            EventId = "65535",
                            TemplateName = "S55OOA",
                            MetadataSections = new[]
                            {
                                new JdeSpecMetadataSection
                                {
                                    Title = "Properties",
                                    Items = new[]
                                    {
                                        new JdeSpecMetadataItem { Label = "Event", Value = "Form Variables [65535]" }
                                    }
                                }
                            }
                        }
                    },
                    Components = new JdeInteractiveComponentSpec[]
                    {
                        new JdeInteractivePushButtonComponentSpec
                        {
                            ControlId = 21,
                            Name = "Find",
                            ComponentType = JdeInteractiveComponentType.PushButton,
                            ComponentTypeLabel = "Push Button",
                            MetadataSections = new[]
                            {
                                new JdeSpecMetadataSection
                                {
                                    Title = "Properties",
                                    Items = new[]
                                    {
                                        new JdeSpecMetadataItem { Label = "Control ID", Value = "21" }
                                    }
                                }
                            }
                        },
                        new JdeInteractiveGridComponentSpec
                        {
                            ControlId = 20,
                            Name = "Grid",
                            ComponentType = JdeInteractiveComponentType.Grid,
                            ComponentTypeLabel = "Grid",
                            MetadataSections = new[]
                            {
                                new JdeSpecMetadataSection
                                {
                                    Title = "Properties",
                                    Items = new[]
                                    {
                                        new JdeSpecMetadataItem { Label = "Control ID", Value = "20" }
                                    }
                                }
                            },
                            Children = new JdeInteractiveComponentSpec[]
                            {
                                new JdeInteractiveGridColumnComponentSpec
                                {
                                    ControlId = 64,
                                    ParentControlId = 20,
                                    Name = "OO PO #",
                                    ComponentType = JdeInteractiveComponentType.GridColumn,
                                    ComponentTypeLabel = "Grid Column",
                                    MetadataSections = new[]
                                    {
                                        new JdeSpecMetadataSection
                                        {
                                            Title = "Properties",
                                            Items = new[]
                                            {
                                                new JdeSpecMetadataItem { Label = "Control ID", Value = "64" }
                                            }
                                        }
                                    },
                                    Events = new[]
                                    {
                                        new JdeInteractiveEventSpec
                                        {
                                            Name = "Grid Column Clicked [9860]",
                                            EventSpecKey = "EV-COL",
                                            EventId = "9860",
                                            MetadataSections = new[]
                                            {
                                                new JdeSpecMetadataSection
                                                {
                                                    Title = "Properties",
                                                    Items = new[]
                                                    {
                                                        new JdeSpecMetadataItem { Label = "Event", Value = "Grid Column Clicked [9860]" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var root = (JdeEventRulesNode)buildTree.Invoke(engine, new object[] { spec })!;

        await Assert.That(root.MetadataSections.Count).IsEqualTo(1);
        await Assert.That(root.HasEventRules).IsFalse();
        await Assert.That(root.Children.Count).IsEqualTo(1);

        var formNode = root.Children[0];
        await Assert.That(formNode.MetadataSections.Count).IsEqualTo(1);
        await Assert.That(formNode.Children[0].Name).IsEqualTo("Events");
        await Assert.That(formNode.Children[0].HasEventRules).IsFalse();
        await Assert.That(formNode.Children[0].Children[0].HasEventRules).IsTrue();
        await Assert.That(formNode.Children[1].Name).IsEqualTo("Find");

        var gridNode = formNode.Children[2];
        await Assert.That(gridNode.Name).IsEqualTo("Grid");
        await Assert.That(gridNode.MetadataSections.Count).IsEqualTo(1);
        await Assert.That(gridNode.HasEventRules).IsFalse();
        await Assert.That(gridNode.Children.Count).IsEqualTo(1);
        await Assert.That(gridNode.Children[0].Name).IsEqualTo("OO PO #");
        await Assert.That(gridNode.Children[0].HasEventRules).IsFalse();
        await Assert.That(gridNode.Children[0].Children[0].Name).IsEqualTo("Events");
        await Assert.That(gridNode.Children[0].Children[0].Children[0].HasEventRules).IsTrue();
    }

    [Test]
    public async Task BuildControlNodes_ApplicationGridIncludesGridEventsAndMetadataOnlyColumns()
    {
        Type engineType = typeof(JdeClient).Assembly.GetType("JdeClient.Core.Internal.EventRulesQueryEngine", throwOnError: true)!;
        Type rowType = engineType.GetNestedType("EventRulesLinkRow", BindingFlags.NonPublic)!;
        Type controlKeyType = engineType.GetNestedType("EventRulesControlKey", BindingFlags.NonPublic)!;
        Type controlMetadataType = engineType.GetNestedType("ApplicationControlMetadata", BindingFlags.NonPublic)!;
        Type sectionKeyType = engineType.GetNestedType("EventRulesSectionKey", BindingFlags.NonPublic)!;
        Type sectionMetadataType = engineType.GetNestedType("ReportSectionMetadata", BindingFlags.NonPublic)!;
        Type treeMetadataType = engineType.GetNestedType("ApplicationTreeMetadata", BindingFlags.NonPublic)!;

        object rows = CreateGenericList(
            rowType,
            CreateInstance(rowType, string.Empty, "S55OOA", 0, "65535", 65535, 0, "EV-FORM"),
            CreateInstance(rowType, string.Empty, "S55OOA", 21, "0", 0, 0, "EV-FIND"),
            CreateInstance(rowType, string.Empty, "S55OOA", 20, "1537", 1537, 0, "EV-GRID"),
            CreateInstance(rowType, string.Empty, "S55OOA", 64, "9860", 9860, 0, "EV-COL"));

        object controlMetadata = CreateGenericDictionary(
            controlKeyType,
            controlMetadataType,
            (
                CreateInstance(controlKeyType, "S55OOA", 20),
                CreateInstance(controlMetadataType, "Grid", "Grid config", null, "Grid", 2231, null, 1, true)
            ),
            (
                CreateInstance(controlKeyType, "S55OOA", 21),
                CreateInstance(controlMetadataType, "Find", "Find config", null, "Push Button", 2232, null, 2, false)
            ),
            (
                CreateInstance(controlKeyType, "S55OOA", 64),
                CreateInstance(controlMetadataType, "OO PO #", "Column 64 config", null, "Grid Column", 2763, 20, 27, false)
            ),
            (
                CreateInstance(controlKeyType, "S55OOA", 65),
                CreateInstance(controlMetadataType, "Status", "Column 65 config", null, "Grid Column", 2764, 20, 28, false)
            ));

        IDictionary formDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["S55OOA"] = "Routing"
        };
        object sectionMetadata = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(sectionKeyType, sectionMetadataType))!;
        object treeMetadata = CreateInstance(
            treeMetadataType,
            "P55OO",
            formDescriptions,
            controlMetadata,
            sectionMetadata);

        MethodInfo buildControlNodes = engineType.GetMethod(
            "BuildControlNodes",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = ((IEnumerable)buildControlNodes.Invoke(null, new[] { rows, (object)1, treeMetadata })!)
            .Cast<JdeEventRulesNode>()
            .ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].Name).IsEqualTo("Form Events");
        await Assert.That(result[1].Name).IsEqualTo("Find");
        await Assert.That(result[2].Name).IsEqualTo("Grid");

        var gridNode = result[2];
        await Assert.That(gridNode.Children.Count).IsEqualTo(3);
        await Assert.That(gridNode.Children[0].Name).IsEqualTo("Events");
        await Assert.That(gridNode.Children[0].NodeType).IsEqualTo(JdeEventRulesNodeType.Section);
        await Assert.That(gridNode.Children[0].Children.Count).IsEqualTo(1);
        await Assert.That(gridNode.Children[0].Children[0].Name).IsEqualTo("Grid Record is Fetched [1537]");
        await Assert.That(gridNode.Children[1].Name).IsEqualTo("OO PO #");
        await Assert.That(gridNode.Children[1].Children.Count).IsEqualTo(1);
        await Assert.That(gridNode.Children[1].Children[0].Name).IsEqualTo("Grid Column Clicked [9860]");
        await Assert.That(gridNode.Children[2].Name).IsEqualTo("Status");
        await Assert.That(gridNode.Children[2].Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ResolveApplicationControlDisplayText_UsesDataDictionaryTitleAndAliasFallbacks()
    {
        Type engineType = typeof(JdeClient).Assembly.GetType("JdeClient.Core.Internal.EventRulesQueryEngine", throwOnError: true)!;
        MethodInfo resolveDisplayText = engineType.GetMethod(
            "ResolveApplicationControlDisplayText",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var attributes = new[]
        {
            new KeyValuePair<string, string>("DataItem", "Y55RPN"),
            new KeyValuePair<string, string>("Table", "F553105A")
        };

        string? ddTitleLabel = (string?)resolveDisplayText.Invoke(
            null,
            new object[]
            {
                3257,
                attributes,
                new Dictionary<int, string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Y55RPN"] = "Parent Number"
                }
            });

        string? aliasLabel = (string?)resolveDisplayText.Invoke(
            null,
            new object[]
            {
                3257,
                attributes,
                new Dictionary<int, string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });

        await Assert.That(ddTitleLabel).IsEqualTo("Parent Number");
        await Assert.That(aliasLabel).IsEqualTo("Y55RPN");
    }

    private static object CreateGenericList(Type itemType, params object[] items)
    {
        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        foreach (object item in items)
        {
            list.Add(item);
        }

        return list;
    }

    private static object CreateGenericDictionary(Type keyType, Type valueType, params (object Key, object Value)[] entries)
    {
        IDictionary dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;
        foreach ((object key, object value) in entries)
        {
            dictionary.Add(key, value);
        }

        return dictionary;
    }

    private static object CreateInstance(Type type, params object?[] args)
    {
        return Activator.CreateInstance(
                   type,
                   BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                   binder: null,
                   args,
                   culture: null)
               ?? throw new InvalidOperationException($"Could not create {type.FullName}.");
    }
}
