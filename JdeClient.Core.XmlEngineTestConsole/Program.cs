// See https://aka.ms/new-console-template for more information

using JdeClient.Core;
using JdeClient.Core.XmlEngine;

const string FILEPATH = "C:\\Users\\dtujo\\Documents\\ERXMLTest\\ER_N55FCOO _ GetBrinellHardness.xml";
var eventXmlString = File.ReadAllText(FILEPATH);

const string FILEPATH2 = "C:\\Users\\dtujo\\Documents\\ERXMLTest\\DSTMPL_N55FCOO _ GetBrinellHardness_T55FCMCHA.xml";
var dsXmlString = File.ReadAllText(FILEPATH2);

using var client = new JdeClient.Core.JdeClient();
await client.ConnectAsync();

var resolver = new JdeSpecResolver(client);
var xmlEngine = new JdeXmlEngine(eventXmlString, dsXmlString, resolver);
xmlEngine.ConvertXmlToReadableEr();

Console.Write(xmlEngine.ReadableEventRule);
