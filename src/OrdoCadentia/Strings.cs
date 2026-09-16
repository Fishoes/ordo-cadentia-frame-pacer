// Strings.cs — every piece of text the user reads, in all three languages.
//
// Each phrase is a property that returns the current language's version. It is
// verbose, but the compiler enforces it: there is no "key not found" at
// run time, and no phrase silently left behind in a translation.
//
// Parameter order is always: (Portuguese, English, Spanish).
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace OrdoCadentia
{
    internal enum Language { Pt, En, Es }

    internal static class Txt
    {
        public static Language Current = Detect();

        /// <summary>The Windows language, if it is one of the three. Otherwise English.</summary>
        public static Language Detect()
        {
            try
            {
                string c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (c == "pt") return Language.Pt;
                if (c == "es") return Language.Es;
            }
            catch { }
            return Language.En;
        }

        public static string Code
        {
            get { return Current == Language.Pt ? "pt" : (Current == Language.Es ? "es" : "en"); }
        }

        public static Language FromCode(string c)
        {
            if (string.IsNullOrEmpty(c)) return Detect();
            c = c.Trim().ToLowerInvariant();
            if (c.StartsWith("pt")) return Language.Pt;
            if (c.StartsWith("es")) return Language.Es;
            if (c.StartsWith("en")) return Language.En;
            return Detect();
        }

        /// <summary>Name of the instruction file matching the current language.</summary>
        public static string InstructionFile
        {
            get { return Current == Language.Pt ? "LEIA-ME.txt"
                       : (Current == Language.Es ? "LEEME.txt" : "README.txt"); }
        }

        static string Pick(string pt, string en, string es)
        {
            switch (Current)
            {
                case Language.Pt: return pt;
                case Language.Es: return es;
                default: return en;
            }
        }

        // ================================================================= product
        public static string Subtitle { get { return Pick(
            "SINCRONIZADOR DE QUADROS", "FRAME PACER", "SINCRONIZADOR DE FOTOGRAMAS"); } }

        // ================================================================= pacing verdict
        public static string CadenciaPerfeita { get { return Pick(
            "CADÊNCIA PERFEITA", "PERFECT PACING", "CADENCIA PERFECTA"); } }
        public static string TremorLeve { get { return Pick(
            "LEVE TREMOR", "SLIGHT JUDDER", "LEVE TEMBLOR"); } }
        public static string TremorVisivel { get { return Pick(
            "TREMOR VISÍVEL", "VISIBLE JUDDER", "TEMBLOR VISIBLE"); } }
        public static string TremorSevero { get { return Pick(
            "TREMOR SEVERO", "SEVERE JUDDER", "TEMBLOR SEVERO"); } }

        // ================================================================= video errors
        public static string VideoOk { get { return Pick("ok", "ok", "ok"); } }
        public static string VideoReiniciar { get { return Pick(
            "exige reiniciar o Windows", "requires restarting Windows",
            "requiere reiniciar Windows"); } }
        public static string VideoModoRuim { get { return Pick(
            "modo não suportado", "unsupported mode", "modo no admitido"); } }
        public static string VideoDriverRecusou { get { return Pick(
            "o driver de vídeo recusou", "the display driver refused",
            "el controlador de video lo rechazo"); } }
        public static string VideoNaoGravou { get { return Pick(
            "não foi possível gravar", "could not be saved", "no se pudo guardar"); } }
        public static string VideoParametros { get { return Pick(
            "parâmetros inválidos", "invalid parameters", "parametros invalidos"); } }
        public static string VideoParametro { get { return Pick(
            "parâmetro inválido", "invalid parameter", "parametro invalido"); } }
        public static string VideoMultiMonitor { get { return Pick(
            "incompatível com múltiplos monitores", "incompatible with multiple displays",
            "incompatible con varios monitores"); } }
        public static string VideoErroN { get { return Pick("erro {0}", "error {0}", "error {0}"); } }

        public static string NaoLiMonitor { get { return Pick(
            "não consegui ler o modo atual do monitor", "could not read the display's current mode",
            "no pude leer el modo actual del monitor"); } }
        public static string MonitorRecusou { get { return Pick(
            "o monitor recusou {0} Hz ({1})", "the display refused {0} Hz ({1})",
            "el monitor rechazo {0} Hz ({1})"); } }
        public static string TaxaInvalida { get { return Pick(
            "taxa inválida", "invalid refresh rate", "frecuencia invalida"); } }
        public static string NaoLiMonitorCurto { get { return Pick(
            "não consegui ler o monitor", "could not read the display",
            "no pude leer el monitor"); } }

        // ================================================================= CPU
        public static string CpuUniforme { get { return Pick(
            "{0} núcleos lógicos (uniforme)", "{0} logical cores (uniform)",
            "{0} nucleos logicos (uniforme)"); } }
        public static string CpuHibrida { get { return Pick(
            "{0} lógicos — {1} de desempenho (P) + {2} de eficiência (E)",
            "{0} logical — {1} performance (P) + {2} efficiency (E)",
            "{0} logicos — {1} de rendimiento (P) + {2} de eficiencia (E)"); } }

        // ================================================================= timer
        public static string TempNaoConsultou { get { return Pick(
            "não consegui consultar o temporizador", "could not query the timer",
            "no pude consultar el temporizador"); } }
        public static string TempRecusado { get { return Pick(
            "o sistema recusou (status {0})", "the system refused (status {0})",
            "el sistema lo rechazo (estado {0})"); } }

        // ================================================================= compatibility
        public static string SemCaminhoExe { get { return Pick(
            "sem caminho do executável", "executable path unknown",
            "sin ruta del ejecutable"); } }
        public static string SemRegistro { get { return Pick(
            "não consegui abrir o registro", "could not open the registry",
            "no pude abrir el registro"); } }

        // ================================================================= window: sections
        public static string SecJanelaJogo { get { return Pick(
            "JANELA DO JOGO", "GAME WINDOW", "VENTANA DEL JUEGO"); } }
        public static string SecQuadros { get { return Pick(
            "QUADROS POR SEGUNDO", "FRAMES PER SECOND", "FOTOGRAMAS POR SEGUNDO"); } }
        public static string SecTaxa { get { return Pick(
            "TAXA DO MONITOR", "DISPLAY REFRESH RATE", "FRECUENCIA DEL MONITOR"); } }
        public static string SecAntesDepois { get { return Pick(
            "ANTES E DEPOIS", "BEFORE AND AFTER", "ANTES Y DESPUES"); } }
        public static string SecAplicar { get { return Pick(
            "O QUE APLICAR", "WHAT TO APPLY", "QUE APLICAR"); } }
        public static string SecHistorico { get { return Pick(
            "HISTÓRICO", "LOG", "REGISTRO"); } }

        // ================================================================= window: labels
        public static string RotFps { get { return Pick(
            "QUADROS QUE O JOGO RENDERIZA", "FRAMES THE GAME RENDERS",
            "FOTOGRAMAS QUE EL JUEGO GENERA"); } }
        public static string RotFg { get { return Pick(
            "GERAÇÃO DE QUADROS (FRAME GEN)", "FRAME GENERATION (FRAME GEN)",
            "GENERACION DE FOTOGRAMAS (FRAME GEN)"); } }
        public static string QuadrosNaTela { get { return Pick(
            "  QUADROS NA TELA", "  FRAMES ON SCREEN", "  FOTOGRAMAS EN PANTALLA"); } }
        public static string NenhumaJanela { get { return Pick(
            "nenhuma janela escolhida", "no window selected",
            "ninguna ventana elegida"); } }
        public static string TelaInteira { get { return Pick(
            "  ·  tela inteira", "  ·  full screen", "  ·  pantalla completa"); } }

        public static string MonitorAceitaFaixa { get { return Pick(
            "SEU MONITOR ACEITA DE {0} A {1} Hz", "YOUR DISPLAY SUPPORTS {0} TO {1} Hz",
            "TU MONITOR ADMITE DE {0} A {1} Hz"); } }
        public static string MonitorAceitaUm { get { return Pick(
            "SEU MONITOR ACEITA {0} Hz", "YOUR DISPLAY SUPPORTS {0} Hz",
            "TU MONITOR ADMITE {0} Hz"); } }
        public static string MonitorSemVarredura { get { return Pick(
            "O MONITOR NÃO RESPONDEU À VARREDURA", "THE DISPLAY DID NOT ANSWER THE SCAN",
            "EL MONITOR NO RESPONDIO AL ANALISIS"); } }
        public static string AgoraEm { get { return Pick(
            "agora em {0} Hz", "now at {0} Hz", "ahora en {0} Hz"); } }
        public static string TravadoEm { get { return Pick(
            "travado em {0} Hz", "locked at {0} Hz", "fijado en {0} Hz"); } }

        // ================================================================= window: buttons
        public static string BtEscolherJanela { get { return Pick(
            "ESCOLHER JANELA", "PICK WINDOW", "ELEGIR VENTANA"); } }
        public static string BtApontar { get { return Pick("APONTAR", "POINT", "APUNTAR"); } }
        public static string BtSoltar { get { return Pick("SOLTAR", "CLEAR", "QUITAR"); } }
        public static string BtAtivar { get { return Pick("ATIVAR", "ACTIVATE", "ACTIVAR"); } }
        public static string BtDesativar { get { return Pick("DESATIVAR", "DEACTIVATE", "DESACTIVAR"); } }
        public static string BtTravarEm { get { return Pick(
            "TRAVAR O MONITOR EM {0} Hz", "LOCK THE DISPLAY AT {0} Hz",
            "FIJAR EL MONITOR EN {0} Hz"); } }
        public static string BtTravar { get { return Pick(
            "TRAVAR O MONITOR", "LOCK THE DISPLAY", "FIJAR EL MONITOR"); } }
        public static string BtDestravar { get { return Pick(
            "DESTRAVAR  (agora em {0} Hz)", "UNLOCK  (now at {0} Hz)",
            "LIBERAR  (ahora en {0} Hz)"); } }

        public static string SubEscolhaJanela { get { return Pick(
            "escolha uma janela primeiro", "pick a window first",
            "elige una ventana primero"); } }
        public static string SubDesfaz { get { return Pick(
            "desfaz tudo e devolve ao normal", "undoes everything and restores your system",
            "deshace todo y devuelve al estado normal"); } }
        public static string SubAplica { get { return Pick(
            "aplica os ajustes marcados", "applies the checked adjustments",
            "aplica los ajustes marcados"); } }

        public static string NotaTravaLigada { get { return Pick(
            "a trava cai sozinha quando você fechar o programa.",
            "the lock releases by itself when you close the program.",
            "el bloqueo se suelta solo cuando cierres el programa."); } }
        public static string NotaTravaDesligada { get { return Pick(
            "travar deixa o monitor nessa taxa mesmo sem jogo escolhido.",
            "locking holds the display at that rate even with no game selected.",
            "fijar mantiene el monitor en esa frecuencia aunque no elijas un juego."); } }

        // ================================================================= window: adjustments
        public static string AjSincronia { get { return Pick(
            "SINCRONIZAR A TELA", "SYNC THE DISPLAY", "SINCRONIZAR LA PANTALLA"); } }
        public static string AjSincroniaDesc { get { return Pick(
            "casa a taxa do monitor com os quadros que chegam nela",
            "matches the refresh rate to the frames reaching the screen",
            "ajusta la frecuencia del monitor a los fotogramas que llegan"); } }

        public static string AjTemporizador { get { return Pick(
            "AFINAR O TEMPORIZADOR", "SHARPEN THE TIMER", "AFINAR EL TEMPORIZADOR"); } }
        public static string AjTemporizadorDesc { get { return Pick(
            "pede ao Windows precisão de 0,5 ms em vez de 15,6 ms",
            "asks Windows for 0.5 ms precision instead of 15.6 ms",
            "pide a Windows precision de 0,5 ms en vez de 15,6 ms"); } }

        public static string AjPrioridade { get { return Pick(
            "PRIORIDADE ALTA", "HIGH PRIORITY", "PRIORIDAD ALTA"); } }
        public static string AjPrioridadeDesc { get { return Pick(
            "põe o jogo na frente na fila do processador",
            "puts the game ahead in the processor queue",
            "pone el juego adelante en la cola del procesador"); } }

        public static string AjNucleos { get { return Pick(
            "NÚCLEOS DE DESEMPENHO", "PERFORMANCE CORES", "NUCLEOS DE RENDIMIENTO"); } }
        public static string AjNucleosDesc { get { return Pick(
            "impede o jogo de escorregar para os núcleos de eficiência",
            "keeps the game from slipping onto the efficiency cores",
            "evita que el juego caiga en los nucleos de eficiencia"); } }

        public static string AjTelaCheia { get { return Pick(
            "DESLIGAR OTIMIZAÇÕES DE TELA CHEIA", "TURN OFF FULLSCREEN OPTIMIZATIONS",
            "DESACTIVAR OPTIMIZACIONES DE PANTALLA COMPLETA"); } }
        public static string AjTelaCheiaDesc { get { return Pick(
            "camada extra do Windows que costuma atrapalhar a cadência",
            "an extra Windows layer that usually hurts pacing",
            "capa extra de Windows que suele estropear la cadencia"); } }

        public static string AjFundo { get { return Pick(
            "REDUZIR PROGRAMAS DE FUNDO", "QUIET BACKGROUND PROGRAMS",
            "REDUCIR PROGRAMAS EN SEGUNDO PLANO"); } }
        public static string AjFundoDesc { get { return Pick(
            "rebaixa navegador, Discord e afins durante a partida",
            "lowers browser, Discord and the like while you play",
            "baja navegador, Discord y similares mientras juegas"); } }

        public static string EstadoAplicado { get { return Pick("aplicado", "applied", "aplicado"); } }
        public static string EstadoProxima { get { return Pick(
            "na próxima", "next launch", "la proxima"); } }
        public static string EstadoParcial { get { return Pick("parcial", "partial", "parcial"); } }
        public static string EstadoTravado { get { return Pick("travado", "locked", "fijado"); } }

        public static string SemNucleosE { get { return Pick(
            "esta CPU não tem núcleos de eficiência", "this CPU has no efficiency cores",
            "esta CPU no tiene nucleos de eficiencia"); } }
        public static string ExeDesconhecido { get { return Pick(
            "caminho do executável desconhecido", "executable path unknown",
            "ruta del ejecutable desconocida"); } }

        // ================================================================= window: footer
        public static string RodapeEscolha { get { return Pick(
            "escolha a janela do jogo para começar.", "pick the game window to start.",
            "elige la ventana del juego para empezar."); } }
        public static string RodapeTravado { get { return Pick(
            "monitor travado em {0} Hz.", "display locked at {0} Hz.",
            "monitor fijado en {0} Hz."); } }
        public static string RodapeResumo { get { return Pick(
            "{0} quadros na tela · tela em {1} Hz · {2}",
            "{0} frames on screen · display at {1} Hz · {2}",
            "{0} fotogramas en pantalla · pantalla en {1} Hz · {2}"); } }
        public static string RodapeMonitor { get { return Pick(
            "monitor: {0}{1}", "display: {0}{1}", "monitor: {0}{1}"); } }
        public static string RodapeMedido { get { return Pick(
            "  (medido {0:F2} Hz)", "  (measured {0:F2} Hz)", "  (medido {0:F2} Hz)"); } }
        public static string RodapeTemporizador { get { return Pick(
            "temporizador: {0:F2} ms", "timer: {0:F2} ms", "temporizador: {0:F2} ms"); } }

        // ================================================================= window: instructions
        public static string ComoUsar { get { return Pick("COMO USAR", "HOW TO USE", "COMO USAR"); } }
        public static string Passo1 { get { return Pick(
            "1. Limite o jogo no FPS que você quer (menu do jogo, RTSS ou driver).",
            "1. Cap the game at the FPS you want (game menu, RTSS or driver).",
            "1. Limita el juego a los FPS que quieras (menu del juego, RTSS o driver)."); } }
        public static string Passo2 { get { return Pick(
            "2. Ligue o VSync no jogo.",
            "2. Turn VSync on in the game.",
            "2. Activa VSync en el juego."); } }
        public static string Passo3 { get { return Pick(
            "3. Escolha a janela do jogo aqui e informe o mesmo FPS.",
            "3. Pick the game window here and enter the same FPS.",
            "3. Elige la ventana del juego aqui e indica el mismo FPS."); } }
        public static string Passo4 { get { return Pick(
            "4. Clique em ATIVAR.", "4. Click ACTIVATE.", "4. Haz clic en ACTIVAR."); } }

        // ================================================================= window: aiming
        public static string MiraTitulo { get { return Pick(
            "APONTE E CLIQUE NA JANELA DO JOGO", "POINT AND CLICK ON THE GAME WINDOW",
            "APUNTA Y HAZ CLIC EN LA VENTANA DEL JUEGO"); } }
        public static string MiraCancela { get { return Pick(
            "Esc cancela", "Esc cancels", "Esc cancela"); } }
        public static string MiraInstrucao { get { return Pick(
            "aponte e clique na janela do jogo (Esc cancela).",
            "point and click on the game window (Esc cancels).",
            "apunta y haz clic en la ventana del juego (Esc cancela)."); } }
        public static string MiraCancelada { get { return Pick(
            "mira cancelada.", "aiming cancelled.", "punteria cancelada."); } }
        public static string MiraSouEu { get { return Pick(
            "essa é a janela do próprio programa. Arraste-a para o lado, ou use ESCOLHER JANELA.",
            "that is this program's own window. Drag it aside, or use PICK WINDOW.",
            "esa es la ventana del propio programa. Muevela a un lado, o usa ELEGIR VENTANA."); } }
        public static string MiraNada { get { return Pick(
            "nada utilizável ali — tente ESCOLHER JANELA.",
            "nothing usable there — try PICK WINDOW.",
            "nada utilizable ahi — prueba ELEGIR VENTANA."); } }

        // ================================================================= window: warnings
        public static string AvisoNaoDivide { get { return Pick(
            "aviso: {0} Hz não divide {1} quadros por segundo — vai sobrar tremor.",
            "warning: {0} Hz does not divide {1} frames per second — judder will remain.",
            "aviso: {0} Hz no divide {1} fotogramas por segundo — quedara temblor."); } }
        public static string AvisoTravadoNaoDivide { get { return Pick(
            "aviso: o monitor está travado em {0} Hz, que não divide {1} quadros por segundo.",
            "warning: the display is locked at {0} Hz, which does not divide {1} frames per second.",
            "aviso: el monitor esta fijado en {0} Hz, que no divide {1} fotogramas por segundo."); } }
        public static string AvisoFgBaixo { get { return Pick(
            "aviso: frame gen sobre base baixa deixa a imagem lisa, mas o controle continua pesado.",
            "warning: frame gen on a low base looks smooth, but the controls still feel heavy.",
            "aviso: frame gen sobre base baja se ve fluido, pero el control sigue pesado."); } }
        public static string AvisoHzMenor { get { return Pick(
            "aviso: a tela a {0} Hz não mostra {1} quadros por segundo.",
            "warning: a display at {0} Hz cannot show {1} frames per second.",
            "aviso: una pantalla a {0} Hz no puede mostrar {1} fotogramas por segundo."); } }

        // ================================================================= window: log
        public static string HistPronto { get { return Pick(
            "{0} {1} — pronto.", "{0} {1} — ready.", "{0} {1} — listo."); } }
        public static string HistMonitorAceita { get { return Pick(
            "monitor: aceita {0} Hz (máximo {1} Hz).",
            "display: supports {0} Hz (maximum {1} Hz).",
            "monitor: admite {0} Hz (maximo {1} Hz)."); } }
        public static string HistSemAdmin { get { return Pick(
            "sem privilégio de administrador: alguns ajustes podem falhar.",
            "not running as administrator: some adjustments may fail.",
            "sin privilegios de administrador: algunos ajustes pueden fallar."); } }
        public static string HistTempGlobal { get { return Pick(
            "temporizador global do Windows ligado — a precisão alcança o jogo.",
            "Windows global timer is on — the precision reaches the game.",
            "temporizador global de Windows activo — la precision llega al juego."); } }
        public static string HistLimpouSobras { get { return Pick(
            "limpei {0} marca(s) de tela cheia que sobraram de um fechamento anormal.",
            "cleaned up {0} fullscreen flag(s) left over from an abnormal shutdown.",
            "limpie {0} marca(s) de pantalla completa de un cierre anormal."); } }
        public static string HistJanelaEscolhida { get { return Pick(
            "janela escolhida: {0} (PID {1}).", "window selected: {0} (PID {1}).",
            "ventana elegida: {0} (PID {1})."); } }
        public static string HistJanelaSolta { get { return Pick(
            "janela solta.", "window cleared.", "ventana liberada."); } }
        public static string HistPodeJogar { get { return Pick(
            "pode ir jogar. minimize esta janela — os ajustes continuam valendo.",
            "you can go play. minimize this window — the adjustments stay active.",
            "ya puedes jugar. minimiza esta ventana — los ajustes siguen activos."); } }
        public static string HistVazio { get { return Pick(
            "nada registrado ainda.", "nothing logged yet.", "nada registrado aun."); } }

        // ================================================================= engine
        public static string MotNenhumaJanela { get { return Pick(
            "nenhuma janela marcada.", "no window selected.", "ninguna ventana elegida."); } }
        public static string MotJanelaSumiu { get { return Pick(
            "a janela marcada não existe mais.", "the selected window no longer exists.",
            "la ventana elegida ya no existe."); } }
        public static string MotAtivado { get { return Pick(
            "— ativado em {0} —", "— activated on {0} —", "— activado en {0} —"); } }
        public static string MotAlvo { get { return Pick(
            "alvo: {0} renderizados × {1} = {2} quadros na tela",
            "target: {0} rendered × {1} = {2} frames on screen",
            "objetivo: {0} generados × {1} = {2} fotogramas en pantalla"); } }
        public static string MotDesativado { get { return Pick(
            "— desativado{0} —", "— deactivated{0} —", "— desactivado{0} —"); } }
        public static string MotFechouJanela { get { return Pick(
            "a janela foi fechada", "the window was closed", "la ventana se cerro"); } }
        public static string MotProgramaFechado { get { return Pick(
            "o programa foi fechado", "the program was closed", "el programa se cerro"); } }
        public static string MotPelaBandeja { get { return Pick(
            "pela bandeja", "from the tray", "desde la bandeja"); } }
        public static string MotTelaDevolvida { get { return Pick(
            "tela devolvida ao estado original.", "display restored to its original state.",
            "pantalla devuelta a su estado original."); } }

        public static string MotTelaMantidaTravada { get { return Pick(
            "tela: mantida travada em {0} Hz — {1}.",
            "display: kept locked at {0} Hz — {1}.",
            "pantalla: se mantiene fijada en {0} Hz — {1}."); } }
        public static string MotSemTaxa { get { return Pick(
            "tela: nenhuma taxa utilizável.", "display: no usable refresh rate.",
            "pantalla: ninguna frecuencia utilizable."); } }
        public static string MotNaoMudou { get { return Pick(
            "tela: não consegui mudar para {0} Hz — {1}",
            "display: could not switch to {0} Hz — {1}",
            "pantalla: no pude cambiar a {0} Hz — {1}"); } }
        public static string MotJaEstava { get { return Pick(
            "tela: já estava em {0} Hz — {1}.", "display: already at {0} Hz — {1}.",
            "pantalla: ya estaba en {0} Hz — {1}."); } }
        public static string MotTrocou { get { return Pick(
            "tela: {0} Hz → {1} Hz — {2}.", "display: {0} Hz → {1} Hz — {2}.",
            "pantalla: {0} Hz → {1} Hz — {2}."); } }
        public static string MotQuadroDura { get { return Pick(
            "   cada quadro passa a durar {0:F2} ms, sempre.",
            "   every frame now lasts {0:F2} ms, always.",
            "   cada fotograma pasa a durar {0:F2} ms, siempre."); } }
        public static string MotTelaDevolvidaA { get { return Pick(
            "tela: devolvida a {0} Hz.", "display: restored to {0} Hz.",
            "pantalla: devuelta a {0} Hz."); } }
        public static string MotContinuaTravada { get { return Pick(
            "tela: continua travada em {0} Hz (trava manual).",
            "display: still locked at {0} Hz (manual lock).",
            "pantalla: sigue fijada en {0} Hz (bloqueo manual)."); } }

        public static string MotNaoTravou { get { return Pick(
            "não consegui travar em {0} Hz — {1}", "could not lock at {0} Hz — {1}",
            "no pude fijar en {0} Hz — {1}"); } }
        public static string MotTravado { get { return Pick(
            "monitor travado em {0} Hz.", "display locked at {0} Hz.",
            "monitor fijado en {0} Hz."); } }
        public static string MotTravadoDe { get { return Pick(
            "monitor: {0} Hz → {1} Hz, e travado aí.",
            "display: {0} Hz → {1} Hz, and locked there.",
            "monitor: {0} Hz → {1} Hz, y fijado ahi."); } }
        public static string MotTravaEnquantoAberto { get { return Pick(
            "   vale enquanto este programa estiver aberto.",
            "   holds while this program stays open.",
            "   vale mientras este programa este abierto."); } }
        public static string MotDestravado { get { return Pick(
            "monitor destravado, de volta a {0} Hz.",
            "display unlocked, back to {0} Hz.",
            "monitor liberado, de vuelta a {0} Hz."); } }

        public static string MotAlgoMudou { get { return Pick(
            "algo mudou a tela para {0} Hz; devolvi para {1} Hz.",
            "something changed the display to {0} Hz; I put it back to {1} Hz.",
            "algo cambio la pantalla a {0} Hz; la devolvi a {1} Hz."); } }
        public static string MotDesistiRepor { get { return Pick(
            "algo insiste em mudar a taxa da tela; parei de repor.",
            "something keeps changing the refresh rate; I stopped putting it back.",
            "algo insiste en cambiar la frecuencia; deje de reponerla."); } }
        public static string MotDesistiMotivo { get { return Pick(
            "   costuma ser jogo em tela cheia exclusiva — use janela sem bordas.",
            "   usually an exclusive-fullscreen game — use borderless windowed instead.",
            "   suele ser un juego en pantalla completa exclusiva — usa ventana sin bordes."); } }

        public static string MotSaiuDoJogo { get { return Pick(
            "saiu do jogo: tela devolvida ao normal.",
            "left the game: display back to normal.",
            "saliste del juego: pantalla de vuelta a lo normal."); } }
        public static string MotVoltouAoJogo { get { return Pick(
            "voltou ao jogo: tela em {0} Hz.", "back in the game: display at {0} Hz.",
            "volviste al juego: pantalla en {0} Hz."); } }

        public static string MotTempFalhou { get { return Pick(
            "temporizador: falhou — {0}", "timer: failed — {0}",
            "temporizador: fallo — {0}"); } }
        public static string MotTempPedido { get { return Pick(
            "temporizador: pedido {0:F2} ms (sistema em {1:F2} ms).",
            "timer: asked for {0:F2} ms (system at {1:F2} ms).",
            "temporizador: pedi {0:F2} ms (sistema en {1:F2} ms)."); } }
        public static string MotTempRestrito { get { return Pick(
            "   o Windows limitou o pedido a este processo (veja o {0}).",
            "   Windows limited the request to this process only (see {0}).",
            "   Windows limito la peticion a este proceso (mira {0})."); } }

        public static string MotPrioridade { get { return Pick(
            "prioridade: {0} → alta.", "priority: {0} → high.", "prioridad: {0} → alta."); } }
        public static string MotPrioridadeFalhou { get { return Pick(
            "prioridade: falhou — {0}", "priority: failed — {0}",
            "prioridad: fallo — {0}"); } }

        public static string MotNucleosNada { get { return Pick(
            "núcleos: esta CPU não tem núcleos de eficiência, nada a fazer.",
            "cores: this CPU has no efficiency cores, nothing to do.",
            "nucleos: esta CPU no tiene nucleos de eficiencia, nada que hacer."); } }
        public static string MotNucleosFixado { get { return Pick(
            "núcleos: fixado nos {0} de desempenho ({1}).",
            "cores: pinned to the {0} performance ones ({1}).",
            "nucleos: fijado en los {0} de rendimiento ({1})."); } }
        public static string MotNucleosPorque { get { return Pick(
            "   sem migrar para os núcleos de eficiência — é lá que nascem os picos.",
            "   no migrating to efficiency cores — that is where the spikes come from.",
            "   sin migrar a los nucleos de eficiencia — es de ahi que vienen los picos."); } }
        public static string MotNucleosFalhou { get { return Pick(
            "núcleos: falhou — {0}", "cores: failed — {0}", "nucleos: fallo — {0}"); } }

        public static string MotTcSemCaminho { get { return Pick(
            "otimizações de tela cheia: não descobri o caminho do executável.",
            "fullscreen optimizations: could not find the executable path.",
            "optimizaciones de pantalla completa: no encontre la ruta del ejecutable."); } }
        public static string MotTcJaDesligadas { get { return Pick(
            "otimizações de tela cheia: já estavam desligadas.",
            "fullscreen optimizations: already turned off.",
            "optimizaciones de pantalla completa: ya estaban desactivadas."); } }
        public static string MotTcDesligadas { get { return Pick(
            "otimizações de tela cheia: desligadas para {0}.",
            "fullscreen optimizations: turned off for {0}.",
            "optimizaciones de pantalla completa: desactivadas para {0}."); } }
        public static string MotTcProxima { get { return Pick(
            "   só vale a partir da PRÓXIMA vez que o jogo abrir.",
            "   only takes effect the NEXT time the game starts.",
            "   solo tiene efecto la PROXIMA vez que abras el juego."); } }
        public static string MotTcFalhou { get { return Pick(
            "otimizações de tela cheia: falhou — {0}",
            "fullscreen optimizations: failed — {0}",
            "optimizaciones de pantalla completa: fallo — {0}"); } }

        public static string MotFundoRebaixados { get { return Pick(
            "programas de fundo: {0} rebaixado(s) de prioridade.",
            "background programs: {0} lowered in priority.",
            "programas en segundo plano: {0} con prioridad reducida."); } }
        public static string MotFundoNada { get { return Pick(
            "programas de fundo: nada pesado em execução.",
            "background programs: nothing heavy running.",
            "programas en segundo plano: nada pesado en ejecucion."); } }
        public static string MotFundoDevolvidos { get { return Pick(
            "programas de fundo: {0} devolvido(s) ao normal.",
            "background programs: {0} restored to normal.",
            "programas en segundo plano: {0} devuelto(s) a lo normal."); } }

        public static string MotSocorro { get { return Pick(
            "o programa fechou de forma anormal da última vez; monitor devolvido a {0} Hz.",
            "the program shut down abnormally last time; display restored to {0} Hz.",
            "el programa se cerro de forma anormal la ultima vez; monitor devuelto a {0} Hz."); } }

        public static string PrioIdle { get { return Pick("ociosa", "idle", "inactiva"); } }
        public static string PrioAbaixo { get { return Pick(
            "abaixo do normal", "below normal", "por debajo de lo normal"); } }
        public static string PrioNormal { get { return Pick("normal", "normal", "normal"); } }
        public static string PrioAcima { get { return Pick(
            "acima do normal", "above normal", "por encima de lo normal"); } }
        public static string PrioAlta { get { return Pick("alta", "high", "alta"); } }
        public static string PrioTempoReal { get { return Pick(
            "tempo real", "real time", "tiempo real"); } }

        public static string ErroAcessoNegado { get { return Pick(
            "acesso negado (abra como administrador)",
            "access denied (run as administrator)",
            "acceso denegado (ejecuta como administrador)"); } }
        public static string ErroProcessoSumiu { get { return Pick(
            "o processo não existe mais", "the process no longer exists",
            "el proceso ya no existe"); } }

        // ================================================================= window picker
        public static string DlgTitulo { get { return Pick(
            "ESCOLHER A JANELA", "PICK THE WINDOW", "ELEGIR LA VENTANA"); } }
        public static string DlgDica { get { return Pick(
            "clique na janela do jogo — clique duplo marca direto",
            "click the game window — double-click selects it right away",
            "haz clic en la ventana del juego — doble clic la elige directo"); } }
        public static string DlgMarcarEsta { get { return Pick(
            "MARCAR ESTA", "SELECT THIS", "ELEGIR ESTA"); } }
        public static string DlgAtualizar { get { return Pick(
            "ATUALIZAR LISTA", "REFRESH LIST", "ACTUALIZAR LISTA"); } }
        public static string DlgCancelar { get { return Pick(
            "CANCELAR", "CANCEL", "CANCELAR"); } }
        public static string DlgVazio { get { return Pick(
            "nenhuma janela candidata encontrada", "no candidate windows found",
            "ninguna ventana candidata encontrada"); } }
        public static string DlgSemCaminho { get { return Pick(
            "caminho do executável indisponível (tente abrir como administrador)",
            "executable path unavailable (try running as administrator)",
            "ruta del ejecutable no disponible (prueba ejecutar como administrador)"); } }

        // ================================================================= pacing meter
        public static string MedAgora { get { return Pick("AGORA", "NOW", "AHORA"); } }
        public static string MedDepoisDeAtivar { get { return Pick(
            "DEPOIS DE ATIVAR", "AFTER ACTIVATING", "DESPUES DE ACTIVAR"); } }
        public static string MedAntes { get { return Pick("ANTES", "BEFORE", "ANTES"); } }
        public static string MedDepois { get { return Pick("DEPOIS", "AFTER", "DESPUES"); } }
        public static string MedVazio { get { return Pick(
            "escolha uma janela para ver a comparação", "pick a window to see the comparison",
            "elige una ventana para ver la comparacion"); } }
        public static string MedConstante { get { return Pick(
            "{0} Hz · {1} FPS · {2:F1} ms constante", "{0} Hz · {1} FPS · {2:F1} ms steady",
            "{0} Hz · {1} FPS · {2:F1} ms constante"); } }
        public static string MedOscila { get { return Pick(
            "{0} Hz · {1} FPS · {2:F1} ↔ {3:F1} ms  (oscila {4:F1} ms)",
            "{0} Hz · {1} FPS · {2:F1} ↔ {3:F1} ms  (swings {4:F1} ms)",
            "{0} Hz · {1} FPS · {2:F1} ↔ {3:F1} ms  (oscila {4:F1} ms)"); } }
        public static string MedLegendaBoa { get { return Pick(
            "{0} quadros em {1:F0} ms — todos com a mesma duração, como num console",
            "{0} frames in {1:F0} ms — all the same length, just like a console",
            "{0} fotogramas en {1:F0} ms — todos con la misma duracion, como una consola"); } }
        public static string MedLegendaRuim { get { return Pick(
            "espera alternada de {0} atualizações por quadro  ·  {1} quadros em {2:F0} ms",
            "alternating wait of {0} refreshes per frame  ·  {1} frames in {2:F0} ms",
            "espera alternada de {0} actualizaciones por fotograma  ·  {1} fotogramas en {2:F0} ms"); } }

        // ================================================================= chips
        public static string FichaConsole { get { return Pick("console", "console", "consola"); } }
        public static string FichaPadrao { get { return Pick("padrão", "standard", "estandar"); } }
        public static string FichaNao { get { return Pick("NÃO", "NO", "NO"); } }
        public static string FichaSemFg { get { return Pick("sem FG", "no FG", "sin FG"); } }
        public static string FichaAuto { get { return Pick("AUTO", "AUTO", "AUTO"); } }
        public static string FichaExato { get { return Pick("exato", "exact", "exacto"); } }
        public static string FichaTremor { get { return Pick("tremor", "judder", "temblor"); } }
        public static string FichaRuim { get { return Pick("ruim", "bad", "malo"); } }

        // ================================================================= tray
        public static string BandejaMostrar { get { return Pick("Mostrar", "Show", "Mostrar"); } }
        public static string BandejaDesativar { get { return Pick(
            "Desativar", "Deactivate", "Desactivar"); } }
        public static string BandejaSair { get { return Pick("Sair", "Exit", "Salir"); } }
        public static string BandejaAtivo { get { return Pick(
            "Os ajustes continuam valendo. Clique duas vezes no ícone para voltar.",
            "The adjustments are still active. Double-click the icon to come back.",
            "Los ajustes siguen activos. Haz doble clic en el icono para volver."); } }
        public static string BandejaTravado { get { return Pick(
            "O monitor continua travado. Clique duas vezes no ícone para voltar.",
            "The display is still locked. Double-click the icon to come back.",
            "El monitor sigue fijado. Haz doble clic en el icono para volver."); } }

        public static string TituloAtivo { get { return Pick("ATIVO", "ACTIVE", "ACTIVO"); } }
        public static string TituloTravado { get { return Pick(
            "MONITOR TRAVADO", "DISPLAY LOCKED", "MONITOR FIJADO"); } }

        // ================================================================= program
        public static string JaAberto { get { return Pick(
            "O Ordo Cadentia já está aberto.\n\nProcure o ícone na bandeja do sistema, ao lado do relógio.",
            "Ordo Cadentia is already running.\n\nLook for the icon in the system tray, next to the clock.",
            "Ordo Cadentia ya esta abierto.\n\nBusca el icono en la bandeja del sistema, junto al reloj."); } }
        public static string DeuErrado { get { return Pick(
            "Algo deu errado e os ajustes foram desfeitos.\n\n{0}",
            "Something went wrong and the adjustments were undone.\n\n{0}",
            "Algo salio mal y los ajustes fueron deshechos.\n\n{0}"); } }

        // ================================================================= installer
        public static string InsTitulo { get { return Pick("INSTALAR", "INSTALL", "INSTALAR"); } }
        public static string InsResumo { get { return Pick(
            "Faz 30 FPS no computador parecerem 30 FPS num videogame.",
            "Makes 30 FPS on a PC feel like 30 FPS on a console.",
            "Hace que 30 FPS en la PC se sientan como 30 FPS en una consola."); } }
        public static string InsOndeInstalar { get { return Pick(
            "ONDE INSTALAR", "INSTALL LOCATION", "DONDE INSTALAR"); } }
        public static string InsMudar { get { return Pick("MUDAR", "CHANGE", "CAMBIAR"); } }
        public static string InsEscolherPasta { get { return Pick(
            "Onde instalar o Ordo Cadentia?", "Where should Ordo Cadentia be installed?",
            "¿Donde instalar Ordo Cadentia?"); } }
        public static string InsOptArea { get { return Pick(
            "Atalho na área de trabalho", "Desktop shortcut",
            "Acceso directo en el escritorio"); } }
        public static string InsOptAreaDesc { get { return Pick(
            "para achar rápido", "so it is easy to find", "para encontrarlo rapido"); } }
        public static string InsOptMenu { get { return Pick(
            "Atalho no menu Iniciar", "Start menu shortcut",
            "Acceso directo en el menu Inicio"); } }
        public static string InsOptMenuDesc { get { return Pick(
            "junto do desinstalador", "along with the uninstaller",
            "junto al desinstalador"); } }
        public static string InsOptLeiaMe { get { return Pick(
            "Abrir as instruções ao terminar", "Open the instructions when done",
            "Abrir las instrucciones al terminar"); } }
        public static string InsOptLeiaMeDesc { get { return Pick(
            "explica o que o programa faz e como usar",
            "explains what the program does and how to use it",
            "explica que hace el programa y como usarlo"); } }
        public static string InsBtInstalar { get { return Pick("INSTALAR", "INSTALL", "INSTALAR"); } }
        public static string InsBtInstalando { get { return Pick(
            "INSTALANDO...", "INSTALLING...", "INSTALANDO..."); } }
        public static string InsBtTentarDeNovo { get { return Pick(
            "TENTAR DE NOVO", "TRY AGAIN", "INTENTAR DE NUEVO"); } }
        public static string InsBtAbrir { get { return Pick(
            "ABRIR O ORDO CADENTIA", "OPEN ORDO CADENTIA", "ABRIR ORDO CADENTIA"); } }
        public static string InsBtLerLeiaMe { get { return Pick(
            "LER AS INSTRUÇÕES", "READ THE INSTRUCTIONS", "LEER LAS INSTRUCCIONES"); } }
        public static string InsBtFechar { get { return Pick("FECHAR", "CLOSE", "CERRAR"); } }
        public static string InsRodape { get { return Pick(
            "Não precisa de administrador. Não instala driver nem serviço.",
            "No administrator needed. Installs no driver and no service.",
            "No necesita administrador. No instala controlador ni servicio."); } }
        public static string InsJaAberto { get { return Pick(
            "O Ordo Cadentia está aberto neste momento.\n\nFeche-o (inclusive o ícone na bandeja, ao lado do relógio) e clique em INSTALAR de novo.",
            "Ordo Cadentia is running right now.\n\nClose it (including the tray icon next to the clock) and click INSTALL again.",
            "Ordo Cadentia esta abierto en este momento.\n\nCierralo (incluido el icono en la bandeja, junto al reloj) y haz clic en INSTALAR de nuevo."); } }
        public static string InsPassoPasta { get { return Pick(
            "preparando a pasta...", "preparing the folder...", "preparando la carpeta..."); } }
        public static string InsPassoGravando { get { return Pick(
            "gravando o programa...", "writing the program...", "escribiendo el programa..."); } }
        public static string InsPassoAtalhoArea { get { return Pick(
            "criando o atalho na área de trabalho...", "creating the desktop shortcut...",
            "creando el acceso directo del escritorio..."); } }
        public static string InsPassoAtalhoMenu { get { return Pick(
            "criando os atalhos do menu Iniciar...", "creating the Start menu shortcuts...",
            "creando los accesos del menu Inicio..."); } }
        public static string InsPassoRegistrando { get { return Pick(
            "registrando no Windows...", "registering with Windows...",
            "registrando en Windows..."); } }
        public static string InsPassoPronto { get { return Pick(
            "pronto.", "done.", "listo."); } }
        public static string InsInstalado { get { return Pick(
            "INSTALADO", "INSTALLED", "INSTALADO"); } }
        public static string InsProximoPasso { get { return Pick(
            "PRÓXIMO PASSO", "NEXT STEP", "SIGUIENTE PASO"); } }
        public static string InsProx1 { get { return Pick(
            "1. Limite seu jogo em 30 FPS e ligue o VSync.",
            "1. Cap your game at 30 FPS and turn VSync on.",
            "1. Limita tu juego a 30 FPS y activa VSync."); } }
        public static string InsProx2 { get { return Pick(
            "2. Abra o Ordo Cadentia e escolha a janela do jogo.",
            "2. Open Ordo Cadentia and pick the game window.",
            "2. Abre Ordo Cadentia y elige la ventana del juego."); } }
        public static string InsProx3 { get { return Pick(
            "3. Clique em ATIVAR.", "3. Click ACTIVATE.", "3. Haz clic en ACTIVAR."); } }
        public static string InsProxLeiaMe { get { return Pick(
            "As instruções completas explicam tudo isso com calma.",
            "The full instructions walk through all of this calmly.",
            "Las instrucciones completas explican todo esto con calma."); } }
        public static string InsNaoDeuCerto { get { return Pick(
            "NÃO DEU CERTO", "IT DID NOT WORK", "NO FUNCIONO"); } }
        public static string InsErroDica { get { return Pick(
            "Tente escolher outra pasta em MUDAR, ou feche o Ordo Cadentia se estiver aberto.",
            "Try another folder with CHANGE, or close Ordo Cadentia if it is open.",
            "Prueba otra carpeta con CAMBIAR, o cierra Ordo Cadentia si esta abierto."); } }
        public static string InsNaoAbriu { get { return Pick(
            "Não consegui abrir:\n\n{0}", "Could not open:\n\n{0}",
            "No pude abrir:\n\n{0}"); } }
        public static string InsDescricaoAtalho { get { return Pick(
            "Sincronizador de quadros", "Frame pacer", "Sincronizador de fotogramas"); } }
        public static string InsDescricaoDesinstalar { get { return Pick(
            "Remove o Ordo Cadentia", "Removes Ordo Cadentia", "Elimina Ordo Cadentia"); } }
        public static string InsAtalhoInstrucoes { get { return Pick(
            "Instruções", "Instructions", "Instrucciones"); } }
        public static string InsIdioma { get { return Pick("IDIOMA", "LANGUAGE", "IDIOMA"); } }

        // ================================================================= uninstaller
        public static string DesTitulo { get { return Pick(
            "DESINSTALAR", "UNINSTALL", "DESINSTALAR"); } }
        public static string DesVaiEmbora { get { return Pick(
            "Vai embora o seguinte:", "The following will be removed:",
            "Se eliminara lo siguiente:"); } }
        public static string DesItem1 { get { return Pick(
            "o programa e a pasta onde ele está", "the program and the folder it lives in",
            "el programa y la carpeta donde esta"); } }
        public static string DesItem2 { get { return Pick(
            "os atalhos da área de trabalho e do menu Iniciar",
            "the desktop and Start menu shortcuts",
            "los accesos directos del escritorio y del menu Inicio"); } }
        public static string DesItem3 { get { return Pick(
            "o registro na lista de programas do Windows",
            "the entry in the Windows list of installed programs",
            "el registro en la lista de programas de Windows"); } }
        public static string DesItem4 { get { return Pick(
            "qualquer marca de tela cheia que ele tenha deixado",
            "any fullscreen flag it may have left behind",
            "cualquier marca de pantalla completa que haya dejado"); } }
        public static string DesOptAjustes { get { return Pick(
            "Apagar também meus ajustes", "Also delete my settings",
            "Borrar tambien mis ajustes"); } }
        public static string DesOptAjustesDesc { get { return Pick(
            "suas escolhas de FPS, taxa e ajustes",
            "your FPS, refresh rate and adjustment choices",
            "tus elecciones de FPS, frecuencia y ajustes"); } }
        public static string DesBtDesinstalar { get { return Pick(
            "DESINSTALAR", "UNINSTALL", "DESINSTALAR"); } }
        public static string DesBtRemovendo { get { return Pick(
            "REMOVENDO...", "REMOVING...", "ELIMINANDO..."); } }
        public static string DesBtSub { get { return Pick(
            "remove o programa e os atalhos", "removes the program and its shortcuts",
            "elimina el programa y sus accesos directos"); } }
        public static string DesJaAberto { get { return Pick(
            "O Ordo Cadentia está aberto neste momento.\n\nFeche-o (inclusive o ícone na bandeja, ao lado do relógio) e clique em DESINSTALAR de novo.",
            "Ordo Cadentia is running right now.\n\nClose it (including the tray icon next to the clock) and click UNINSTALL again.",
            "Ordo Cadentia esta abierto en este momento.\n\nCierralo (incluido el icono en la bandeja, junto al reloj) y haz clic en DESINSTALAR de nuevo."); } }
        public static string DesPassoRegistro { get { return Pick(
            "desfazendo marcas no registro...", "undoing registry flags...",
            "deshaciendo marcas en el registro..."); } }
        public static string DesPassoAtalhos { get { return Pick(
            "removendo atalhos...", "removing shortcuts...",
            "eliminando accesos directos..."); } }
        public static string DesPassoLista { get { return Pick(
            "saindo da lista de programas...", "leaving the program list...",
            "saliendo de la lista de programas..."); } }
        public static string DesPassoAjustes { get { return Pick(
            "apagando os ajustes...", "deleting the settings...",
            "borrando los ajustes..."); } }
        public static string DesPassoArquivos { get { return Pick(
            "removendo os arquivos...", "removing the files...",
            "eliminando los archivos..."); } }
        public static string DesRemovido { get { return Pick(
            "REMOVIDO", "REMOVED", "ELIMINADO"); } }
        public static string DesRemovidoN { get { return Pick(
            "{0} item(ns) removido(s). A pasta some em instantes.",
            "{0} item(s) removed. The folder disappears in a moment.",
            "{0} elemento(s) eliminado(s). La carpeta desaparece en un momento."); } }
        public static string DesNadaAlterado { get { return Pick(
            "Seu monitor e suas configurações do Windows ficaram como estavam.",
            "Your display and your Windows settings were left as they were.",
            "Tu monitor y tu configuracion de Windows quedaron como estaban."); } }
        public static string DesAjustesMantidos { get { return Pick(
            "Seus ajustes foram mantidos, caso volte.",
            "Your settings were kept, in case you come back.",
            "Tus ajustes se conservaron, por si vuelves."); } }

        // ================================================================= shared
        public static string NaoDeuCerto { get { return Pick(
            "NÃO DEU CERTO", "IT DID NOT WORK", "NO FUNCIONO"); } }

        // ================================================================= persistence
        /// <summary>
        /// Le so a linha de idioma do ajustes.ini. O instalador e o desinstalador
        /// need it without loading the whole Settings class.
        /// </summary>
        public static void LoadFromDisk(string pastaAjustes)
        {
            try
            {
                string arq = Path.Combine(pastaAjustes, "settings.ini");
                if (!File.Exists(arq)) return;
                foreach (var linha in File.ReadAllLines(arq, Encoding.UTF8))
                {
                    int i = linha.IndexOf('=');
                    if (i <= 0) continue;
                    if (linha.Substring(0, i).Trim() == "Language")
                    {
                        Current = FromCode(linha.Substring(i + 1).Trim());
                        return;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Writes the language choice while preserving the rest of the file. The installer
        /// usa isto: sobrescrever o ajustes.ini inteiro apagaria as escolhas de
        /// someone who already used the program and is merely reinstalling.
        /// </summary>
        public static void SaveToDisk(string pastaAjustes, Language idioma)
        {
            try
            {
                Directory.CreateDirectory(pastaAjustes);
                string arq = Path.Combine(pastaAjustes, "settings.ini");
                string codigo = idioma == Language.Pt ? "pt" : (idioma == Language.Es ? "es" : "en");

                var linhas = File.Exists(arq)
                    ? new System.Collections.Generic.List<string>(
                          File.ReadAllLines(arq, Encoding.UTF8))
                    : new System.Collections.Generic.List<string>();

                bool achou = false;
                for (int i = 0; i < linhas.Count; i++)
                {
                    int p = linhas[i].IndexOf('=');
                    if (p > 0 && linhas[i].Substring(0, p).Trim() == "Language")
                    {
                        linhas[i] = "Language=" + codigo;
                        achou = true;
                        break;
                    }
                }
                if (!achou) linhas.Add("Language=" + codigo);

                File.WriteAllLines(arq, linhas.ToArray(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
