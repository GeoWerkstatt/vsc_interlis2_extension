using Geowerkstatt.Interlis.Tools.AST;
using Geowerkstatt.Interlis.Tools.AST.Types;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// INTERLIS AST visitor to generate a Mermaid class diagram script.
/// Follows a two-pass approach within topics to handle Mermaid namespace limitations.
/// </summary>
internal class DiagramDocumentVisitor : Interlis24AstBaseVisitor<object?>
{
    private readonly StringBuilder mermaidScript = new StringBuilder();

    private static readonly Dictionary<(long? Min, long? Max), string> MermaidCardinalityMap = new()
    {
        { (0, 1),   "\"0..1\" " },
        { (1, 1),   "\"1\" "    },
        { (0, null), "\"*\""    },
        { (1, null), "\"1..*\" " }
    };

    public DiagramDocumentVisitor()
    {
        mermaidScript.AppendLine("classDiagram");
        mermaidScript.AppendLine("direction LR");
    }

    public override object? VisitInterlisFile([NotNull] InterlisFile interlisFile)
    {
        foreach (var model in interlisFile.Content.Values)
        {
            ((IAstElement)model).Accept(this);
        }
        return null;
    }

    public override object? VisitModelDef([NotNull] ModelDef modelDef)
    {
        foreach (var content in modelDef.Content.Values)
        {
            ((IAstElement)content).Accept(this);
        }
        return null;
    }

    public override object? VisitTopicDef([NotNull] TopicDef topicDef)
    {
        mermaidScript.AppendLine($"namespace Topic_{topicDef.Name} {{");

        var classesInTopic = new List<ClassDef>();
        var associationsInTopic = new List<AssociationDef>();

        foreach (var content in topicDef.Content.Values)
        {
            if (content is ClassDef classDef)
            {
                mermaidScript.AppendLine($"  class {classDef.Name}");
                classesInTopic.Add(classDef);
            }
            else if (content is AssociationDef associationDef)
            {
                associationsInTopic.Add(associationDef);
            }
        }

        mermaidScript.AppendLine("}");
        mermaidScript.AppendLine();

        foreach (var classDef in classesInTopic)
        {
            if (classDef.Extends?.Target?.Name is string extendedClassName)
            {
                 mermaidScript.AppendLine($"{classDef.Name} --|> {extendedClassName}");
            }

            if (classDef.MetaAttributes.TryGetValue("geow.uml.color", out var classColor) && !string.IsNullOrWhiteSpace(classColor))
            {
                mermaidScript.AppendLine($"style {classDef.Name} fill:{classColor},color:black,stroke:black");
            }

            foreach (var content in classDef.Content.Values)
            {
                 if (content is AttributeDef attrDef)
                 {
                     AppendAttributeDetails(attrDef);
                 }
            }
             mermaidScript.AppendLine();
        }

        foreach (var associationDef in associationsInTopic)
        {
            AppendAssociationDetails(associationDef);
        }
        mermaidScript.AppendLine();

        return null;
    }

    private void AppendAttributeDetails(AttributeDef attributeDef)
    {
        if (attributeDef.Parent is ClassDef parentClass)
        {
            var typeString = VisitTypeDefInternal(attributeDef.TypeDef as TypeDef);
            mermaidScript.AppendLine($"{parentClass.Name}: +{typeString} {attributeDef.Name}");
        }
    }

     private void AppendAssociationDetails(AssociationDef associationDef)
    {
        var roles = associationDef.Content.Values.OfType<AttributeDef>().ToList();

        if (roles.Count == 2)
        {
            var (firstClass, firstCardinality) = GetClassAndCardinality(roles[0]);
            var (secondClass, secondCardinality) = GetClassAndCardinality(roles[1]);

            if (firstClass != null && secondClass != null && firstCardinality != null && secondCardinality != null)
            {
                string secondCard = secondCardinality.EndsWith(" ") ? secondCardinality : secondCardinality + " ";
                // Append association outside namespace block
                mermaidScript.AppendLine($"{firstClass.Name} {firstCardinality}--o {secondCard}{secondClass.Name} : {associationDef.Name}");
            }
            else { /* Log warning? */ }
        }
        else { /* Log warning for non-binary? */ }
    }


    private string VisitTypeDefInternal(TypeDef? type)
    {
        if (type == null) return "?";
        return type switch
        {
            ReferenceType rt => rt.Target.Value?.Path.Last() ?? "?",
            TextType tt => tt.Length == null ? "Text" : $"Text [{tt.Length}]",
            NumericType nt => nt.Min != null && nt.Max != null ? $"{nt.Min}..{nt.Max}" : "Numeric",
            BooleanType => "Boolean",
            BlackboxType bt => bt.Kind switch {
                BlackboxType.BlackboxTypeKind.Binary => "Blackbox (Binary)",
                BlackboxType.BlackboxTypeKind.Xml => "Blackbox (XML)",
                _ => "Blackbox",
            },
            EnumerationType et => $"({FormatEnumerationValues(et.Values)})",
            TypeRef tr => tr.Extends?.Path.Last() ?? "?",
            RoleType => "Role",
            _ => type.GetType().Name,
        };
    }

    private static string FormatEnumerationValues(EnumerationValuesList enumerationValues)
    {
       return string.Join(", ", enumerationValues.Select(v => v.Name));
    }

    private static (ClassDef? classDef, string? Cardinality) GetClassAndCardinality(AttributeDef? attribute)
    {
        if (attribute?.TypeDef is not RoleType roleType) return (null, null);
        var classDef = roleType.Targets.FirstOrDefault()?.Value?.Target as ClassDef;
        var cardinalityTuple = (roleType.Cardinality.Min, roleType.Cardinality.Max);
        MermaidCardinalityMap.TryGetValue(cardinalityTuple, out string? cardinality);
        return (classDef, cardinality);
    }

    public string GetDiagramDocument()
    {
        return mermaidScript.ToString();
    }
}
