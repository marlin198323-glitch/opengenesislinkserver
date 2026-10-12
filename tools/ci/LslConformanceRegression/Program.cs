using System;
using System.Linq;
using System.Reflection;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.LSL_Types;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("LSL conformance: " + message);
    }

    private static void CheckBinding(string name)
    {
        MethodInfo[] contract = typeof(ILSL_Api).GetMethods().Where(m => m.Name == name).ToArray();
        Check(contract.Length > 0, "No API declaration for " + name);
        foreach (MethodInfo method in contract)
        {
            Type[] parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
            MethodInfo implementation = typeof(LSL_Api).GetMethod(name, parameters);
            MethodInfo wrapper = typeof(ScriptBaseClass).GetMethod(name, parameters);
            Check(implementation != null, "Missing implementation signature " + name);
            Check(wrapper != null, "Missing script wrapper signature " + name);
            Check(implementation.ReturnType == method.ReturnType, "Implementation return type changed " + name);
            Check(wrapper.ReturnType == method.ReturnType, "Script wrapper return type changed " + name);
        }
    }

    private static int Main()
    {
        foreach (string name in new[]
        {
            "llAbs", "llCeil", "llFloor", "llRound", "llFabs",
            "llSay", "llRegionSay", "llEstateSay", "llGetTime",
            "llRequestPermissions", "llOwnerSay", "llGetFreeMemory",
        })
        {
            CheckBinding(name);
        }

        // Stateless LSL_Api math is executed, not merely source-inspected.
        LSL_Api api = new LSL_Api();
        Check(api.llAbs(new LSLInteger(-7)).value == 7, "llAbs(-7) must be 7");
        Check(api.llAbs(new LSLInteger(int.MinValue)).value == int.MinValue,
            "llAbs(INT_MIN) must use LSL wrapping semantics");
        Check(api.llCeil(-1.2).value == -1, "llCeil(-1.2)");
        Check(api.llFloor(-1.2).value == -2, "llFloor(-1.2)");
        Check(api.llRound(2.5).value == 3, "llRound(2.5) must round away from zero");
        Check(api.llRound(-2.5).value == -3, "llRound(-2.5) must round away from zero");
        Check(Math.Abs(api.llFabs(-2.5).value - 2.5) < 0.000001, "llFabs(-2.5)");
        Console.WriteLine("OGL LSL: 12 source signatures + 7 actual math semantics: PASS");
        return 0;
    }
}
