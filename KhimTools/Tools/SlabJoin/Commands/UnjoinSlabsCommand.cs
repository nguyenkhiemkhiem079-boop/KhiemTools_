using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.SlabJoin.Commands
{
    /// <summary>
    /// "Unjoin Slabs" — alias gọi JoinSlabsCommand cùng dialog 4 lựa chọn.
    /// Giữ lại class riêng để ribbon có thể đăng ký 2 nút (Join / Unjoin) riêng biệt
    /// và user vẫn có thể nhận biết ý định từ icon/tooltip.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class UnjoinSlabsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Delegate hoàn toàn sang JoinSlabsCommand — dialog sẽ hiện 4 lựa chọn
            return new JoinSlabsCommand().Execute(commandData, ref message, elements);
        }
    }
}
