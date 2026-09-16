// LanguageTest.cs — walks the string table in all three languages by reflection.
//
// Without this, a phrase missed in one translation would only show up when someone
// switched language and found a blank space on screen.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OrdoCadentia;

static class LanguageTest
{
    static int failures;

    static void Check(string what, bool ok, string detail)
    {
        Console.WriteLine(string.Format("  [{0}] {1}{2}", ok ? "OK " : "FAIL", what,
            string.IsNullOrEmpty(detail) ? "" : "  -> " + detail));
        if (!ok) failures++;
    }

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("THREE-LANGUAGE TEST");

        var props = typeof(Txt).GetProperties(BindingFlags.Public | BindingFlags.Static)
                               .Where(p => p.PropertyType == typeof(string)
                                        && p.GetIndexParameters().Length == 0
                                        && p.Name != "Code" && p.Name != "InstructionFile")
                               .OrderBy(p => p.Name).ToList();

        Console.WriteLine("  phrases in the table: " + props.Count);
        Check("the table is not empty", props.Count > 50, props.Count + " phrases");

        var languages = new[] { Language.Pt, Language.En, Language.Es };
        var names = new[] { "pt", "en", "es" };
        var values = new Dictionary<string, string[]>();

        // ---------------------------------------------------- collect
        for (int i = 0; i < languages.Length; i++)
        {
            Txt.Current = languages[i];
            foreach (var p in props)
            {
                string v;
                try { v = (string)p.GetValue(null, null); }
                catch (Exception ex) { v = null; Console.WriteLine("  EXCEPTION in " + p.Name + ": " + ex.Message); }
                if (!values.ContainsKey(p.Name)) values[p.Name] = new string[3];
                values[p.Name][i] = v;
            }
        }

        // ---------------------------------------------------- empty ones
        Console.WriteLine();
        Console.WriteLine("== BLANK OR NULL PHRASES ===================================");
        var blank = new List<string>();
        foreach (var kv in values)
            for (int i = 0; i < 3; i++)
                if (string.IsNullOrWhiteSpace(kv.Value[i]))
                    blank.Add(kv.Key + " [" + names[i] + "]");
        foreach (var v in blank.Take(10)) Console.WriteLine("    " + v);
        Check("no blank phrase in any of the three languages", blank.Count == 0,
                 blank.Count == 0 ? null : blank.Count + " blank");

        // ---------------------------------------------------- forgotten translation
        // If pt == en == es and the phrase has letters, someone probably pasted the
        // same thing into all three fields. Words genuinely identical in all three
        // languages (AUTO, ok, normal) go in the exception list.
        Console.WriteLine();
        Console.WriteLine("== POSSIBLY FORGOTTEN TRANSLATION ==========================");
        var sameOnPurpose = new HashSet<string>
        {
            "FichaAuto", "VideoOk", "PrioNormal", "EstadoParcial", "EstadoAplicado",
            "DlgCancelar", "InsTitulo", "InsBtInstalar", "DesTitulo", "DesBtDesinstalar",
            "MiraCancela", "ComoUsar", "FichaConsole", "PrioAlta"
        };
        var suspects = new List<string>();
        foreach (var kv in values)
        {
            if (sameOnPurpose.Contains(kv.Key)) continue;
            var v = kv.Value;
            if (v[0] == v[1] && v[1] == v[2] && v[0] != null && v[0].Any(char.IsLetter)
                && v[0].Length > 6)
                suspects.Add(kv.Key + " = \"" + v[0] + "\"");
        }
        foreach (var s in suspects) Console.WriteLine("    " + s);
        Check("no phrase identical across all three languages", suspects.Count == 0,
                 suspects.Count == 0 ? null : suspects.Count + " suspect(s)");

        // ---------------------------------------------------- format placeholders
        // {0}, {1}... must exist in all three, or string.Format breaks or
        // silently drops the number.
        Console.WriteLine();
        Console.WriteLine("== MISMATCHED {0} PLACEHOLDERS =============================");
        var mismatched = new List<string>();
        foreach (var kv in values)
        {
            var slots = new int[3];
            for (int i = 0; i < 3; i++)
            {
                var m = System.Text.RegularExpressions.Regex.Matches(kv.Value[i] ?? "", @"\{(\d+)");
                var indexes = new HashSet<int>();
                foreach (System.Text.RegularExpressions.Match x in m)
                    indexes.Add(int.Parse(x.Groups[1].Value));
                slots[i] = indexes.Count == 0 ? 0 : indexes.Max() + 1;
            }
            if (slots[0] != slots[1] || slots[1] != slots[2])
                mismatched.Add(string.Format("{0}: pt={1} en={2} es={3}",
                    kv.Key, slots[0], slots[1], slots[2]));
        }
        foreach (var d in mismatched) Console.WriteLine("    " + d);
        Check("format placeholders match across the three languages", mismatched.Count == 0,
                 mismatched.Count == 0 ? null : mismatched.Count + " mismatch(es)");

        // ---------------------------------------------------- code and file
        Console.WriteLine();
        Console.WriteLine("== LANGUAGE CODE AND INSTRUCTION FILE ======================");
        for (int i = 0; i < 3; i++)
        {
            Txt.Current = languages[i];
            Console.WriteLine(string.Format("    {0}: code={1}  file={2}",
                languages[i], Txt.Code, Txt.InstructionFile));
            Check("language code for " + languages[i] + " is correct", Txt.Code == names[i], Txt.Code);
        }
        Txt.Current = Language.Pt;
        Check("pt points at LEIA-ME.txt", Txt.InstructionFile == "LEIA-ME.txt", null);
        Txt.Current = Language.En;
        Check("en points at README.txt", Txt.InstructionFile == "README.txt", null);
        Txt.Current = Language.Es;
        Check("es points at LEEME.txt", Txt.InstructionFile == "LEEME.txt", null);

        Check("FromCode understands pt-BR", Txt.FromCode("pt-BR") == Language.Pt, null);
        Check("FromCode understands en-US", Txt.FromCode("en-US") == Language.En, null);
        Check("FromCode understands es-MX", Txt.FromCode("es-MX") == Language.Es, null);

        // ---------------------------------------------------- visible sample
        Console.WriteLine();
        Console.WriteLine("== SAMPLE ==================================================");
        string[] sample = { "BtAtivar", "SecTaxa", "AjSincronia", "CadenciaPerfeita", "InsResumo" };
        foreach (var nome in sample)
        {
            if (!values.ContainsKey(nome)) continue;
            var v = values[nome];
            Console.WriteLine(string.Format("  {0,-18} pt: {1}", nome, v[0]));
            Console.WriteLine(string.Format("  {0,-18} en: {1}", "", v[1]));
            Console.WriteLine(string.Format("  {0,-18} es: {1}", "", v[2]));
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 62));
        Console.WriteLine(failures == 0 ? "LANGUAGES: ALL PASSED" : failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
