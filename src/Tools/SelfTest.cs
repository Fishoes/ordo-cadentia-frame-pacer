// SelfTest.cs — exercises the engine with no UI. Validates P/Invoke, struct offsets and the math.
using System;
using System.Collections.Generic;
using System.Linq;
using OrdoCadentia;

static class Autoteste
{
    static int falhas = 0;

    static void Conferir(string oque, bool ok, string detalhe)
    {
        Console.WriteLine(string.Format("  [{0}] {1}{2}", ok ? "OK " : "FALHA", oque,
            string.IsNullOrEmpty(detalhe) ? "" : "  -> " + detalhe));
        if (!ok) falhas++;
    }

    static void Title(string t)
    {
        Console.WriteLine();
        Console.WriteLine("== " + t + " " + new string('=', Math.Max(0, 62 - t.Length)));
    }

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("AUTOTESTE DO MOTOR FRAME PACER");

        // ---------------------------------------------------------- PACING
        Title("CADENCIA");
        var c165 = Cadence.Analyze(165, 30);
        Console.WriteLine(string.Format("  165Hz/30fps: razao={0:F3} padrao={1} min={2:F2}ms max={3:F2}ms osc={4:F2}ms nota={5} [{6}]",
            c165.Ratio, c165.Pattern, c165.MinMs, c165.MaxMs, c165.SwingMs, c165.Score, c165.Verdict));
        Conferir("165/30 nao e perfeita", !c165.Perfect, null);
        Conferir("165/30 alterna 5 e 6 atualizacoes",
                 c165.MinRefreshes == 5 && c165.MaxRefreshes == 6, c165.Pattern);
        Conferir("165/30 oscila ~6,06 ms", Math.Abs(c165.SwingMs - 6.0606) < 0.01,
                 c165.SwingMs.ToString("F4"));

        var c120 = Cadence.Analyze(120, 30);
        Console.WriteLine(string.Format("  120Hz/30fps: razao={0:F3} padrao={1} min={2:F2}ms max={3:F2}ms osc={4:F2}ms nota={5} [{6}]",
            c120.Ratio, c120.Pattern, c120.MinMs, c120.MaxMs, c120.SwingMs, c120.Score, c120.Verdict));
        Conferir("120/30 e perfeita", c120.Perfect, null);
        Conferir("120/30 sem oscilacao", Math.Abs(c120.SwingMs) < 1e-9, null);
        Conferir("120/30 nota 100", c120.Score == 100, c120.Score.ToString());
        Conferir("120/30 quadro de 33,33 ms", Math.Abs(c120.MinMs - 33.3333) < 0.01, c120.MinMs.ToString("F4"));

        var c60 = Cadence.Analyze(60, 30);
        Conferir("60/30 e perfeita", c60.Perfect && c60.SwingMs < 1e-9, null);
        Conferir("60/30 quadro de 33,33 ms", Math.Abs(c60.MinMs - 33.3333) < 0.01, c60.MinMs.ToString("F4"));

        var c75 = Cadence.Analyze(75, 30);
        Console.WriteLine(string.Format("  75Hz/30fps:  padrao={0} osc={1:F2}ms nota={2}", c75.Pattern, c75.SwingMs, c75.Score));
        Conferir("75/30 alterna 2 e 3", c75.MinRefreshes == 2 && c75.MaxRefreshes == 3, c75.Pattern);

        var c100 = Cadence.Analyze(100, 30);
        Console.WriteLine(string.Format("  100Hz/30fps: padrao={0} osc={1:F2}ms nota={2}", c100.Pattern, c100.SwingMs, c100.Score));

        var c40 = Cadence.Analyze(120, 40);
        Conferir("120/40 e perfeita (modo 40 FPS de console)", c40.Perfect, c40.Pattern);
        var c60_60 = Cadence.Analyze(60, 60);
        Conferir("60/60 e perfeita", c60_60.Perfect && Math.Abs(c60_60.MinMs - 16.6667) < 0.01, null);
        Conferir("nota de 165/30 e pior que a de 120/30", c165.Score < c120.Score,
                 c165.Score + " < " + c120.Score);
        Conferir("entrada invalida nao quebra", Cadence.Analyze(0, 30).Score == 0, null);

        // ---------------------------------------------------------- DISPLAY
        Title("TELA");
        string disp = Display.DeviceForWindow(IntPtr.Zero);
        Console.WriteLine("  dispositivo: " + (disp ?? "(padrao)"));
        var atual = Display.CurrentMode(disp);
        Conferir("leu o modo atual", atual != null, atual == null ? null : atual.ToString());
        if (atual != null)
        {
            var taxas = Display.AvailableRates(disp, atual.Width, atual.Height);
            Console.WriteLine("  taxas: " + string.Join(", ", taxas.Select(t => t + "Hz").ToArray()));
            Conferir("enumerou taxas", taxas.Count > 0, taxas.Count + " modos");
            Conferir("a taxa atual esta na lista", taxas.Contains(atual.Hz), atual.Hz + "Hz");

            foreach (int fps in new[] { 30, 40, 60 })
            {
                int melhor = Display.BestRate(taxas, fps);
                var an = Cadence.Analyze(melhor, fps);
                Console.WriteLine(string.Format("  alvo {0} FPS -> melhor taxa {1}Hz (nota {2}, {3})",
                    fps, melhor, an.Score, an.Verdict));
                Conferir("melhor taxa para " + fps + " FPS e exata",
                         melhor > 0 && melhor % fps == 0, melhor + "Hz");
            }
            double real = Display.MeasureRealRate();
            Console.WriteLine(string.Format("  taxa real medida (DwmFlush): {0:F4} Hz  [nominal {1}]", real, atual.Hz));
            Conferir("DWM devolveu taxa plausivel", real > 20 && real < 1000, real.ToString("F3"));
            Conferir("taxa do DWM bate com a nominal", Math.Abs(real - atual.Hz) < 2.0,
                     string.Format("{0:F3} vs {1}", real, atual.Hz));
        }

        // ---------------------------------------------------------- CPU
        Title("CPU");
        Console.WriteLine("  " + Cpu.Summary());
        UIntPtr mP; int lp, le;
        bool hibrida = Cpu.Map(out mP, out lp, out le);
        Console.WriteLine("  hibrida=" + hibrida + "  logicosP=" + lp + "  logicosE=" + le);
        if (hibrida)
        {
            Console.WriteLine("  mascara P: 0x" + mP.ToUInt64().ToString("X") + "  (" + Cpu.MaskToText(mP) + ")");
            Conferir("mascara P nao vazia", mP.ToUInt64() != 0, null);
            Conferir("P + E = total de logicos", lp + le == Environment.ProcessorCount,
                     lp + "+" + le + " vs " + Environment.ProcessorCount);
            Conferir("ha nucleos E detectados", le > 0, le.ToString());
            int bits = 0; ulong v = mP.ToUInt64();
            while (v != 0) { bits += (int)(v & 1); v >>= 1; }
            Conferir("bits da mascara P == logicosP", bits == lp, bits + " vs " + lp);
        }
        else Console.WriteLine("  (CPU uniforme — o rito de nucleos fica indisponivel)");

        // ---------------------------------------------------------- TIMER
        Title("RELOGIO");
        double mn, mx, at;
        SysTimer.Query(out mn, out mx, out at);
        Console.WriteLine(string.Format("  mais grosso={0:F4}ms  mais fino={1:F4}ms  atual={2:F4}ms", mn, mx, at));
        Conferir("leu a resolucao do temporizador", mn > 0 && mx > 0 && at > 0, null);
        Conferir("o mais fino e <= que o atual", mx <= at + 1e-9, null);

        double obtido; string erro;
        bool elevou = SysTimer.Raise(out obtido, out erro);
        Console.WriteLine(string.Format("  elevar -> {0}  pedida={1:F4}ms  global={2:F4}ms  restritoAoProcesso={3}  {4}",
            elevou, SysTimer.RequestedMs, obtido, SysTimer.ProcessScopedOnly, erro ?? ""));
        Conferir("elevou a resolucao", elevou, erro);
        Conferir("pediu o periodo mais fino (0,5 ms)", Math.Abs(SysTimer.RequestedMs - 0.5) < 1e-6,
                 SysTimer.RequestedMs.ToString("F4"));
        Conferir("relogio global ficou <= 1,0 ms", obtido <= 1.0 + 1e-9, obtido.ToString("F4"));
        Conferir("detectou corretamente o escopo do pedido",
                 SysTimer.ProcessScopedOnly == (obtido > SysTimer.RequestedMs + 1e-6), null);
        SysTimer.Restore();
        SysTimer.Query(out mn, out mx, out at);
        Console.WriteLine(string.Format("  apos restaurar: atual={0:F4}ms", at));
        Console.WriteLine("  GlobalTimerResolutionRequests ativo: " + SysTimer.GlobalEnabled());

        // ---------------------------------------------------------- TARGET
        Title("ALVO");
        var janelas = Target.List();
        Console.WriteLine("  janelas candidatas: " + janelas.Count);
        foreach (var j in janelas.Take(12))
            Console.WriteLine(string.Format("    pid {0,-6} {1,-26} {2}x{3}  {4}",
                j.Pid, j.Process, j.Width, j.Height,
                j.Title.Length > 40 ? j.Title.Substring(0, 37) + "..." : j.Title));
        Conferir("achou alguma janela", janelas.Count > 0, null);
        Conferir("nenhuma janela sem processo", janelas.All(j => !string.IsNullOrEmpty(j.Process)), null);
        Conferir("nenhuma janela e do proprio Frame Pacer",
                 janelas.All(j => j.Process.IndexOf("framepacer", StringComparison.OrdinalIgnoreCase) < 0), null);
        int comCaminho = janelas.Count(j => !string.IsNullOrEmpty(j.ExePath));
        Console.WriteLine("  janelas com caminho do exe resolvido: " + comCaminho + "/" + janelas.Count);

        var primeira = janelas.FirstOrDefault();
        if (primeira != null)
        {
            Conferir("De(hwnd) reencontra a janela", Target.De(primeira.Hwnd) != null, null);
            Conferir("IsAlive confirma", Target.IsAlive(primeira), null);
            Console.WriteLine("  ocupa monitor inteiro: " + Target.FillsMonitor(primeira));
        }

        // ---------------------------------------------------------- COMPAT
        Title("COMPATIBILIDADE (tela cheia)");
        string exeFalso = @"C:\__frame_pacer_teste__\jogo.exe";
        string e2;
        bool antes = Compat.IsOn(exeFalso);
        Conferir("comeca desligado", !antes, null);
        Conferir("liga", Compat.Set(exeFalso, true, out e2), e2);
        Conferir("consulta confirma ligado", Compat.IsOn(exeFalso), null);
        Conferir("ligar de novo e inofensivo", Compat.Set(exeFalso, true, out e2) && Compat.IsOn(exeFalso), e2);
        Conferir("desliga", Compat.Set(exeFalso, false, out e2), e2);
        Conferir("consulta confirma desligado", !Compat.IsOn(exeFalso), null);
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                   @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
            Conferir("valor removido do registro (sem lixo)",
                     k == null || k.GetValue(exeFalso) == null, null);

        // preserves flags set by other tools
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                   @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
        {
            k.SetValue(exeFalso, "~ RUNASADMIN", Microsoft.Win32.RegistryValueKind.String);
            Compat.Set(exeFalso, true, out e2);
            string v1 = (string)k.GetValue(exeFalso);
            Conferir("preservou RUNASADMIN ao ligar", v1 != null && v1.Contains("RUNASADMIN"), v1);
            Compat.Set(exeFalso, false, out e2);
            string v2 = (string)k.GetValue(exeFalso);
            Conferir("preservou RUNASADMIN ao desligar", v2 != null && v2.Contains("RUNASADMIN")
                     && !v2.Contains("DISABLEDXMAXIMIZED"), v2);
            k.DeleteValue(exeFalso, false);
        }

        // ---------------------------------------------------------- result
        Console.WriteLine();
        Console.WriteLine(new string('=', 66));
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " TESTE(S) FALHARAM");
        return falhas == 0 ? 0 : 1;
    }
}
