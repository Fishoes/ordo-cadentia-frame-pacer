// LanguageTest.cs — walks the string table in all three languages by reflection.
//
// Without this, a phrase missed in one translation would only show up when someone
// switched language and found a blank space on screen.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OrdoCadentia;

static class TesteIdiomas
{
    static int falhas;

    static void Conferir(string oque, bool ok, string detalhe)
    {
        Console.WriteLine(string.Format("  [{0}] {1}{2}", ok ? "OK " : "FALHA", oque,
            string.IsNullOrEmpty(detalhe) ? "" : "  -> " + detalhe));
        if (!ok) falhas++;
    }

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("TESTE DOS TRES IDIOMAS");

        var props = typeof(Txt).GetProperties(BindingFlags.Public | BindingFlags.Static)
                               .Where(p => p.PropertyType == typeof(string)
                                        && p.GetIndexParameters().Length == 0
                                        && p.Name != "Code" && p.Name != "InstructionFile")
                               .OrderBy(p => p.Name).ToList();

        Console.WriteLine("  frases na tabela: " + props.Count);
        Conferir("a tabela nao esta vazia", props.Count > 50, props.Count + " frases");

        var idiomas = new[] { Language.Pt, Language.En, Language.Es };
        var nomes = new[] { "pt", "en", "es" };
        var valores = new Dictionary<string, string[]>();

        // ---------------------------------------------------- collect
        for (int i = 0; i < idiomas.Length; i++)
        {
            Txt.Current = idiomas[i];
            foreach (var p in props)
            {
                string v;
                try { v = (string)p.GetValue(null, null); }
                catch (Exception ex) { v = null; Console.WriteLine("  EXCECAO em " + p.Name + ": " + ex.Message); }
                if (!valores.ContainsKey(p.Name)) valores[p.Name] = new string[3];
                valores[p.Name][i] = v;
            }
        }

        // ---------------------------------------------------- empty ones
        Console.WriteLine();
        Console.WriteLine("== FRASES VAZIAS OU NULAS ==================================");
        var vazias = new List<string>();
        foreach (var kv in valores)
            for (int i = 0; i < 3; i++)
                if (string.IsNullOrWhiteSpace(kv.Value[i]))
                    vazias.Add(kv.Key + " [" + nomes[i] + "]");
        foreach (var v in vazias.Take(10)) Console.WriteLine("    " + v);
        Conferir("nenhuma frase vazia nos tres idiomas", vazias.Count == 0,
                 vazias.Count == 0 ? null : vazias.Count + " vazia(s)");

        // ---------------------------------------------------- forgotten translation
        // If pt == en == es and the phrase has letters, someone probably pasted the
        // same thing into all three fields. Words genuinely identical in all three
        // languages (AUTO, ok, normal) go in the exception list.
        Console.WriteLine();
        Console.WriteLine("== POSSIVEL TRADUCAO ESQUECIDA =============================");
        var iguaisOk = new HashSet<string>
        {
            "FichaAuto", "VideoOk", "PrioNormal", "EstadoParcial", "EstadoAplicado",
            "DlgCancelar", "InsTitulo", "InsBtInstalar", "DesTitulo", "DesBtDesinstalar",
            "MiraCancela", "ComoUsar", "FichaConsole", "PrioAlta"
        };
        var suspeitas = new List<string>();
        foreach (var kv in valores)
        {
            if (iguaisOk.Contains(kv.Key)) continue;
            var v = kv.Value;
            if (v[0] == v[1] && v[1] == v[2] && v[0] != null && v[0].Any(char.IsLetter)
                && v[0].Length > 6)
                suspeitas.Add(kv.Key + " = \"" + v[0] + "\"");
        }
        foreach (var s in suspeitas) Console.WriteLine("    " + s);
        Conferir("nenhuma frase identica nos tres idiomas", suspeitas.Count == 0,
                 suspeitas.Count == 0 ? null : suspeitas.Count + " suspeita(s)");

        // ---------------------------------------------------- format placeholders
        // {0}, {1}... must exist in all three, or string.Format breaks or
        // silently drops the number.
        Console.WriteLine();
        Console.WriteLine("== MARCADORES {0} DESIGUAIS ================================");
        var desiguais = new List<string>();
        foreach (var kv in valores)
        {
            var marcas = new int[3];
            for (int i = 0; i < 3; i++)
            {
                var m = System.Text.RegularExpressions.Regex.Matches(kv.Value[i] ?? "", @"\{(\d+)");
                var indices = new HashSet<int>();
                foreach (System.Text.RegularExpressions.Match x in m)
                    indices.Add(int.Parse(x.Groups[1].Value));
                marcas[i] = indices.Count == 0 ? 0 : indices.Max() + 1;
            }
            if (marcas[0] != marcas[1] || marcas[1] != marcas[2])
                desiguais.Add(string.Format("{0}: pt={1} en={2} es={3}",
                    kv.Key, marcas[0], marcas[1], marcas[2]));
        }
        foreach (var d in desiguais) Console.WriteLine("    " + d);
        Conferir("marcadores de formato batem nos tres idiomas", desiguais.Count == 0,
                 desiguais.Count == 0 ? null : desiguais.Count + " divergencia(s)");

        // ---------------------------------------------------- code and file
        Console.WriteLine();
        Console.WriteLine("== CODIGO E ARQUIVO DE INSTRUCOES ==========================");
        for (int i = 0; i < 3; i++)
        {
            Txt.Current = idiomas[i];
            Console.WriteLine(string.Format("    {0}: codigo={1}  arquivo={2}",
                idiomas[i], Txt.Code, Txt.InstructionFile));
            Conferir("codigo de " + idiomas[i] + " correto", Txt.Code == nomes[i], Txt.Code);
        }
        Txt.Current = Language.Pt;
        Conferir("pt aponta para LEIA-ME.txt", Txt.InstructionFile == "LEIA-ME.txt", null);
        Txt.Current = Language.En;
        Conferir("en aponta para README.txt", Txt.InstructionFile == "README.txt", null);
        Txt.Current = Language.Es;
        Conferir("es aponta para LEEME.txt", Txt.InstructionFile == "LEEME.txt", null);

        Conferir("FromCode entende pt-BR", Txt.FromCode("pt-BR") == Language.Pt, null);
        Conferir("FromCode entende en-US", Txt.FromCode("en-US") == Language.En, null);
        Conferir("FromCode entende es-MX", Txt.FromCode("es-MX") == Language.Es, null);

        // ---------------------------------------------------- visible sample
        Console.WriteLine();
        Console.WriteLine("== AMOSTRA ================================================");
        string[] amostra = { "BtAtivar", "SecTaxa", "AjSincronia", "CadenciaPerfeita", "InsResumo" };
        foreach (var nome in amostra)
        {
            if (!valores.ContainsKey(nome)) continue;
            var v = valores[nome];
            Console.WriteLine(string.Format("  {0,-18} pt: {1}", nome, v[0]));
            Console.WriteLine(string.Format("  {0,-18} en: {1}", "", v[1]));
            Console.WriteLine(string.Format("  {0,-18} es: {1}", "", v[2]));
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 62));
        Console.WriteLine(falhas == 0 ? "IDIOMAS: TUDO PASSOU" : falhas + " FALHA(S)");
        return falhas == 0 ? 0 : 1;
    }
}
