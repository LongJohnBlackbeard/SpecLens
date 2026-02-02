using System.Diagnostics.CodeAnalysis;

namespace JdeClient.Core.Models;

public class JdeUserDefinedCodeTypes
{

    public string? ProductCode { get; set; }
    public string? UserDefinedCodeType { get; set; }
    public string? Description { get; set; }
    public string? CodeLength { get; set; }

    [SetsRequiredMembers]
    public JdeUserDefinedCodeTypes()
    {
        
    }
    
    public JdeUserDefinedCodeTypes(string? productCode, string? userDefinedCodeType, string? description, string? codeLength)
    {
        ProductCode = productCode;
        UserDefinedCodeType = userDefinedCodeType;
        Description = description;
        CodeLength = codeLength;
    }
    
}