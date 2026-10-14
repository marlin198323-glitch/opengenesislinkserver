using System;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Yengine;

internal static class Program
{
    private static int checks;

    private static void Check(bool test, string description)
    {
        ++checks;
        if (!test)
            throw new Exception("Firestorm Bridge http_request: " + description);
    }

    private static int Main()
    {
        object[] native = { "a1", "URL_REQUEST_GRANTED", "https://example.invalid/" };
        object[] noChange = XMRInstance.NormalizeHttpRequestArgs(native);
        Check(ReferenceEquals(native, noChange),
            "Native CLR-string event must keep its arguments");

        object[] legacy =
        {
            new LSL_Types.LSLString("b2"),
            new LSL_Types.LSLString("URL_REQUEST_GRANTED"),
            new LSL_Types.LSLString("https://example.invalid/bridge")
        };
        object[] converted = XMRInstance.NormalizeHttpRequestArgs(legacy);
        Check(!ReferenceEquals(legacy, converted),
            "Mixed or LSL-wrapped event requires a new argument array");
        Check(converted[0] is string && (string)converted[0] == "b2",
            "Legacy key must become native string");
        Check(converted[1] is string && (string)converted[1] == "URL_REQUEST_GRANTED",
            "Legacy method must become native string");
        Check(converted[2] is string &&
              (string)converted[2] == "https://example.invalid/bridge",
            "Legacy body must become native string");
        Check(legacy[0] is LSL_Types.LSLString,
            "Original arguments must not be mutated");

        object[] mixed = { "c3", new LSL_Types.LSLString("POST"), "hello" };
        object[] mixedConverted = XMRInstance.NormalizeHttpRequestArgs(mixed);
        Check(ReferenceEquals(mixedConverted[0], mixed[0]) &&
              (string)mixedConverted[1] == "POST" &&
              ReferenceEquals(mixedConverted[2], mixed[2]),
            "Mixed legacy and CLR-string payloads must be preserved exactly");

        object[] wrongCount = { new LSL_Types.LSLString("id") };
        Check(ReferenceEquals(XMRInstance.NormalizeHttpRequestArgs(wrongCount),
                              wrongCount),
            "Do not silently rewrite malformed argument count");
        Check(XMRInstance.NormalizeHttpRequestArgs(null) == null,
            "Null argument array does not throw");

        Check(TypeCast.EHArgUnwrapString(
            new LSL_Types.LSLString("expected")) == "expected",
            "Compiled handler converter supports boxed LSLString");
        Check(TypeCast.EHArgUnwrapString("expected") == "expected",
            "Compiled handler converter supports CLR string");
        Console.WriteLine("Firestorm Bridge http_request parameter regression: PASS (" +
                          checks + " checks)");
        return 0;
    }
}
