using BinaryKits.Zpl.Label.Elements;

namespace BinaryKits.Zpl.Viewer.CommandAnalyzers
{
    /// <summary>
    /// ^LT Label Top: shifts all label content vertically by the given number of dots
    /// </summary>
    public class LabelTopZplCommandAnalyzer : ZplCommandAnalyzerBase
    {
        public LabelTopZplCommandAnalyzer() : base("^LT") { }

        ///<inheritdoc/>
        public override ZplElementBase Analyze(string zplCommand, VirtualPrinter virtualPrinter, IPrinterStorage printerStorage)
        {
            string[] zplDataParts = this.SplitCommand(zplCommand);

            int labelTop = 0;
            if (zplDataParts.Length > 0 && int.TryParse(zplDataParts[0].Trim(), out int tmpint))
            {
                labelTop = tmpint;
            }

            virtualPrinter.SetLabelTop(labelTop);

            return null;
        }
    }
}
