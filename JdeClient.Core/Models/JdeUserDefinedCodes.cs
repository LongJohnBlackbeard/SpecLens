namespace JdeClient.Core.Models;

public class JdeUserDefinedCodes
{
    public string? ProductCode { get; set; }
    public string? UserDefinedCodeType { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Description2 { get; set; }
    public string? SpecialHandlingCode { get; set; }
    public string? HardCoded { get; set; }
    
    public JdeUserDefinedCodes(string? productCode, string? userDefinedCodeType, string? code, string? description,
        string? description2, string? specialHandlingCode, string? hardCoded)
    {
        ProductCode = productCode;
        UserDefinedCodeType = userDefinedCodeType;
        Code = code;
        Description = description;
        Description2 = description2;
        SpecialHandlingCode = specialHandlingCode;
        HardCoded = hardCoded;
    }
}