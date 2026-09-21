namespace FiscalDoc.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        StartupTrace.Log.MainEntered();

        // Gerado pelo SDK a partir das propriedades do projeto. Aplica
        // EnableVisualStyles, SetCompatibleTextRenderingDefault(false) e
        // SetHighDpiMode(PerMonitorV2). O analisador do WinForms exige que o
        // DPI venha por aqui e nao pelo manifesto. Ver plano 3.2.
        ApplicationConfiguration.Initialize();

        // Rede de seguranca: uma falha inesperada tem de virar mensagem, nunca
        // uma janela que some sem explicacao. O usuario abriu um arquivo por
        // duplo clique - se nada aparecer, ele nao tem como saber o que houve.
        Application.ThreadException += (_, e) => Relatar(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Relatar(e.ExceptionObject as Exception);

        // Selecao multipla no Explorer pode mandar varios caminhos. Uma janela,
        // um documento: abre o primeiro e ignora o resto. Ver plano 9.5.
        string? caminhoInicial = args.Length > 0 ? args[0] : null;

        Application.Run(new MainForm(caminhoInicial));
    }

    private static void Relatar(Exception? ex)
    {
        MessageBox.Show(
            "O FiscalDoc encontrou um erro inesperado e precisa fechar."
            + Environment.NewLine + Environment.NewLine
            + (ex?.Message ?? "Erro desconhecido."),
            "FiscalDoc",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
