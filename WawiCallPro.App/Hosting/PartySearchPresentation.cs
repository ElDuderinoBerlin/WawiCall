using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Hosting;

internal sealed class PartySearchPresentation(AppUiState state) : IPartySearchPresentation
{
    public void SizeButtons(params SimpleButton[] buttons) => UiButtonSizing.Apply(buttons);
    public void Restore(GridView view) => state.Restore(view, "PartySearch_v1");
    public void Save(GridView view) => state.Save(view, "PartySearch_v1");
}
