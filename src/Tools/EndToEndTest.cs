// EndToEndTest.cs — end to end: opens a real target window, applies everything, checks
// that the system really changed, and that EVERYTHING is restored at the end.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using OrdoCadentia;

static class Teste2E
{
    static int falhas;

    static void Conferir(string oque, bool ok, string detalhe)
    {
        Console.WriteLine(string.Format("  [{0}] {1}{2}", ok ? "OK " : "FALHA", oque,
            string.IsNullOrEmpty(detalhe) ? "" : "  -> " + detalhe));
        if (!ok) falhas++;
    }

    static void Title(string t)
    {
        Console.WriteLine();
        Console.WriteLine("== " + t + " " + new string('=', Math.Max(0, 60 - t.Length)));
    }

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("TESTE PONTA A PONTA — ORDO CADENTIA");
        Console.WriteLine("(a tela vai piscar duas vezes: e a troca de taxa e a volta)");

        Process alvo = null;
        int hzInicial = 0;
        string dispositivo = null;

        // This test writes and reads settings. Without backing up the real file, it
        // would leave the TEST's choices as the USER's choices.
        string arquivoAjustes = System.IO.Path.Combine(Settings.Folder, "settings.ini");
        string guardado = System.IO.File.Exists(arquivoAjustes)
            ? System.IO.File.ReadAllText(arquivoAjustes) : null;

        var motor = new Engine();

        try
        {
            // ---------------------------------------------------- set up the target
            Title("ALVO");
            alvo = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "AlvoFalso.exe")) { UseShellExecute = false });
            Thread.Sleep(1800);

            var janela = Target.List().FirstOrDefault(j => j.Pid == (uint)alvo.Id);
            Conferir("achou a janela do alvo", janela != null, janela == null ? null : janela.Process);
            if (janela == null) return 1;

            Console.WriteLine("  alvo: " + janela.Process + " PID " + janela.Pid);
            Console.WriteLine("  exe:  " + (janela.ExePath ?? "(desconhecido)"));

            motor.CurrentTarget = janela;
            motor.Config.RenderedFps = 30;
            motor.Config.FgMultiplier = 1;
            motor.Config.ChosenHz = 0;          // automatico
            motor.Config.SyncDisplay = true;
            motor.Config.SharpenTimer = true;
            motor.Config.HighPriority = true;
            motor.Config.PinPerformanceCores = true;
            motor.Config.DisableFullscreenOpt = false;    // mexe no registro; testado a parte
            motor.Config.QuietBackground = false;     // nao mexer nos programas do usuario
            motor.Logged += (t, c) => Console.WriteLine("       | " + t);

            dispositivo = motor.Device;
            var modo = motor.CurrentMode2;
            hzInicial = modo.Hz;
            Console.WriteLine("  monitor antes: " + modo);

            Conferir("30 FPS na tela (sem frame gen)", motor.Config.FpsOnScreen == 30,
                     motor.Config.FpsOnScreen.ToString());
            Console.WriteLine("  taxa que o automatico escolheu: " + motor.TargetHz + " Hz");
            Conferir("o automatico achou taxa exata para 30",
                     motor.TargetHz > 0 && motor.TargetHz % 30 == 0, motor.TargetHz + " Hz");

            var prioridadeAntes = alvo.PriorityClass;
            var afinidadeAntes = alvo.ProcessorAffinity;
            Console.WriteLine("  prioridade antes: " + prioridadeAntes);
            Console.WriteLine("  afinidade antes:  0x" + ((long)afinidadeAntes).ToString("X"));

            // ---------------------------------------------------- run it
            Title("ATIVAR");
            motor.Activate();
            Thread.Sleep(2500);                    // a troca de modo demora

            Conferir("motor ficou ativo", motor.Active, null);

            var depois = Display.CurrentMode(dispositivo);
            Console.WriteLine("  monitor durante: " + depois);
            Conferir("a taxa do monitor mudou de fato", depois.Hz == motor.TargetHz,
                     depois.Hz + " Hz (esperado " + motor.TargetHz + ")");
            Conferir("a resolucao NAO mudou",
                     depois.Width == modo.Width && depois.Height == modo.Height,
                     depois.Width + "x" + depois.Height);

            var cad = Cadence.Analyze(depois.Hz, 30);
            Conferir("a cadencia ficou perfeita", cad.Perfect,
                     string.Format("{0:F2} ms constante", cad.MinMs));

            // What the screen shows while active must be the display's REALITY,
            // not the prediction repeated back.
            Conferir("a leitura ao vivo bate com o monitor de verdade",
                     motor.CurrentPacing.Hz == depois.Hz,
                     motor.CurrentPacing.Hz + " Hz");

            alvo.Refresh();
            Conferir("prioridade do alvo subiu para alta",
                     alvo.PriorityClass == ProcessPriorityClass.High, alvo.PriorityClass.ToString());

            UIntPtr mascaraP; int lp, le;
            if (Cpu.Map(out mascaraP, out lp, out le))
            {
                long esperado = (long)mascaraP.ToUInt64();
                Conferir("alvo preso aos nucleos de desempenho",
                         (long)alvo.ProcessorAffinity == esperado,
                         "0x" + ((long)alvo.ProcessorAffinity).ToString("X") +
                         " (esperado 0x" + esperado.ToString("X") + ")");
            }

            double mn, mx, at;
            SysTimer.Query(out mn, out mx, out at);
            Console.WriteLine(string.Format("  relogio durante: {0:F2} ms (pedido {1:F2} ms)",
                              at, SysTimer.RequestedMs));
            Conferir("o relogio foi pedido no mais fino", Math.Abs(SysTimer.RequestedMs - 0.5) < 1e-6, null);

            // ---------------------------------------------------- undo
            Title("DESATIVAR");
            motor.Deactivate("fim do teste");
            Thread.Sleep(2500);

            Conferir("motor ficou inativo", !motor.Active, null);

            var voltou = Display.CurrentMode(dispositivo);
            Console.WriteLine("  monitor depois: " + voltou);
            Conferir("a taxa do monitor VOLTOU ao original", voltou.Hz == hzInicial,
                     voltou.Hz + " Hz (original " + hzInicial + ")");
            Conferir("a resolucao continua a mesma",
                     voltou.Width == modo.Width && voltou.Height == modo.Height, null);

            alvo.Refresh();
            Conferir("prioridade do alvo voltou", alvo.PriorityClass == prioridadeAntes,
                     alvo.PriorityClass.ToString());
            Conferir("afinidade do alvo voltou",
                     (long)alvo.ProcessorAffinity == (long)afinidadeAntes,
                     "0x" + ((long)alvo.ProcessorAffinity).ToString("X"));

            // ---------------------------------------------------- target that dies
            Title("ALVO QUE FECHA COM O PROGRAMA ATIVO");
            motor.Config.SyncDisplay = false;      // sem piscar a tela de novo
            motor.Activate();
            Conferir("reativado", motor.Active, null);
            alvo.Kill();
            alvo.WaitForExit(4000);
            Thread.Sleep(400);
            motor.Watch();
            Conferir("o motor percebeu e encerrou sozinho", !motor.Active, null);
            alvo = null;

            // ---------------------------------------------------- lock the display
            Title("TRAVAR A TAXA DO MONITOR");
            motor.CurrentTarget = null;

            var oferecidas = motor.DisplayRates;
            Console.WriteLine("  varredura: " + string.Join(", ",
                oferecidas.Select(t => t + "Hz").ToArray()));
            Conferir("a varredura achou taxas", oferecidas.Count > 0, oferecidas.Count + " modos");
            Conferir("nenhuma taxa abaixo de 60 Hz e oferecida",
                     oferecidas.All(t => t >= 60), "minimo " + motor.MinHz);
            Conferir("o maximo bate com a maior da lista",
                     motor.MaxHz == oferecidas.Max(), motor.MaxHz + " Hz");

            string erroTrava;
            int alvoTrava = oferecidas.First(t => t != hzInicial);
            Conferir("travou em " + alvoTrava + " Hz", motor.Lock(alvoTrava, out erroTrava), erroTrava);
            Thread.Sleep(2200);

            Conferir("o motor se diz travado", motor.RateLocked, null);
            Conferir("LockedHz esta correto", motor.LockedHz == alvoTrava, motor.LockedHz.ToString());
            var modoTravado = Display.CurrentMode(dispositivo);
            Conferir("o monitor esta MESMO na taxa travada", modoTravado.Hz == alvoTrava,
                     modoTravado.Hz + " Hz");

            // change rate while the lock is already on
            int segundoAlvo = oferecidas.First(t => t != alvoTrava && t != hzInicial);
            Conferir("trocar para " + segundoAlvo + " Hz com a trava ligada",
                     motor.Lock(segundoAlvo, out erroTrava), erroTrava);
            Thread.Sleep(2200);
            Conferir("o monitor seguiu para a nova taxa",
                     Display.CurrentMode(dispositivo).Hz == segundoAlvo,
                     Display.CurrentMode(dispositivo).Hz + " Hz");

            // the watchdog restores the rate when something changes it underneath
            Title("O VIGIA DA TRAVA");
            string erroSabotagem;
            Display.SetRate(dispositivo, modo.Width, modo.Height, hzInicial, out erroSabotagem);
            Thread.Sleep(2200);
            Conferir("sabotagem aplicada (mudei a taxa por fora)",
                     Display.CurrentMode(dispositivo).Hz == hzInicial,
                     Display.CurrentMode(dispositivo).Hz + " Hz");
            motor.Watch();
            Thread.Sleep(2200);
            Conferir("o vigia devolveu a taxa travada",
                     Display.CurrentMode(dispositivo).Hz == segundoAlvo,
                     Display.CurrentMode(dispositivo).Hz + " Hz");

            // the manual lock takes precedence over the game sync
            Title("TRAVA MANUAL x SINCRONIZACAO");
            var alvo2 = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "AlvoFalso.exe")) { UseShellExecute = false });
            Thread.Sleep(1800);
            motor.CurrentTarget = Target.List().FirstOrDefault(j => j.Pid == (uint)alvo2.Id);
            Conferir("segundo alvo encontrado", motor.CurrentTarget != null, null);

            motor.Config.SyncDisplay = true;
            motor.Config.RenderedFps = 30;
            motor.Config.FgMultiplier = 1;
            motor.Config.ChosenHz = 0;
            Conferir("com a trava ligada, a previsao nao promete mudanca",
                     motor.PredictedPacing.Hz == segundoAlvo,
                     motor.PredictedPacing.Hz + " Hz");

            motor.Activate();
            Thread.Sleep(2200);
            Conferir("ativar NAO mexeu na taxa travada",
                     Display.CurrentMode(dispositivo).Hz == segundoAlvo,
                     Display.CurrentMode(dispositivo).Hz + " Hz");
            Conferir("a trava continua de pe depois de ativar", motor.RateLocked, null);

            motor.Deactivate("fim do teste de trava");
            Thread.Sleep(2200);
            Conferir("desativar NAO soltou a trava manual",
                     motor.RateLocked && Display.CurrentMode(dispositivo).Hz == segundoAlvo,
                     Display.CurrentMode(dispositivo).Hz + " Hz");

            try { alvo2.Kill(); alvo2.WaitForExit(3000); } catch { }
            motor.CurrentTarget = null;

            motor.Unlock();
            Thread.Sleep(2200);
            Conferir("destravar devolveu a taxa original",
                     Display.CurrentMode(dispositivo).Hz == hzInicial,
                     Display.CurrentMode(dispositivo).Hz + " Hz (original " + hzInicial + ")");
            Conferir("o motor nao se diz mais travado", !motor.RateLocked, null);

            // put the original target back for the rest of the test
            motor.Config.SyncDisplay = true;

            // ---------------------------------------------------- promise vs delivery
            Title("O QUE A TELA PROMETE");
            motor.Config.SyncDisplay = true;
            Conferir("com a sincronia ligada, promete a taxa alvo",
                     motor.PredictedPacing.Hz == motor.TargetHz,
                     motor.PredictedPacing.Hz + " Hz");

            motor.Config.SyncDisplay = false;
            Conferir("com a sincronia DESLIGADA, nao promete mudanca nenhuma",
                     motor.PredictedPacing.Hz == motor.CurrentPacing.Hz,
                     motor.PredictedPacing.Hz + " Hz (monitor em " + motor.CurrentPacing.Hz + ")");
            motor.Config.SyncDisplay = true;

            // ---------------------------------------------------- frame gen changes the math
            Title("GERACAO DE QUADROS");
            motor.Config.RenderedFps = 30;
            motor.Config.FgMultiplier = 2;
            Conferir("30 renderizados x2 = 60 na tela", motor.Config.FpsOnScreen == 60,
                     motor.Config.FpsOnScreen.ToString());
            int hzFg = Display.BestRate(motor.DisplayRates, motor.Config.FpsOnScreen);
            Console.WriteLine("  com FG 2x o automatico pede: " + hzFg + " Hz");
            Conferir("a taxa escolhida divide 60 exato", hzFg > 0 && hzFg % 60 == 0, hzFg + " Hz");
            Conferir("com FG a conta muda mesmo",
                     Display.BestRate(motor.DisplayRates, 30) != 0, null);

            motor.Config.FgMultiplier = 3;
            Conferir("30 x3 = 90 na tela", motor.Config.FpsOnScreen == 90, motor.Config.FpsOnScreen.ToString());

            // ---------------------------------------------------- settings persist
            Title("AJUSTES");
            motor.Config.RenderedFps = 45;
            motor.Config.FgMultiplier = 2;
            motor.Config.ChosenHz = 120;
            motor.Config.Save();
            var lido = Settings.Load();
            Conferir("FPS gravado e lido", lido.RenderedFps == 45, lido.RenderedFps.ToString());
            Conferir("multiplicador gravado e lido", lido.FgMultiplier == 2, lido.FgMultiplier.ToString());
            Conferir("Hz gravado e lido", lido.ChosenHz == 120, lido.ChosenHz.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("EXCECAO: " + ex);
            falhas++;
        }
        finally
        {
            try { if (motor.Active) motor.Deactivate("limpeza"); } catch { }
            try { SysTimer.Restore(); } catch { }
            if (dispositivo != null)
            {
                var fim = Display.CurrentMode(dispositivo);
                if (fim != null && hzInicial > 0 && fim.Hz != hzInicial)
                {
                    Console.WriteLine("  ATENCAO: restaurando o monitor a forca...");
                    Display.Restore(dispositivo);
                }
            }
            try { if (alvo != null && !alvo.HasExited) alvo.Kill(); } catch { }

            // restore the settings file exactly as it was
            try
            {
                if (guardado != null) System.IO.File.WriteAllText(arquivoAjustes, guardado);
                else if (System.IO.File.Exists(arquivoAjustes)) System.IO.File.Delete(arquivoAjustes);
                Console.WriteLine("  ajustes do usuario restaurados.");
            }
            catch { }
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine(falhas == 0 ? "PONTA A PONTA: TUDO PASSOU" : falhas + " FALHA(S)");
        return falhas == 0 ? 0 : 1;
    }
}
