namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Ánh xạ loại thép cần dùng -> tên family Rebar Shape tuỳ chỉnh (bộ JP_T## theo BS 8666:2005).
    /// Đầy đủ theo thư mục "2.1_Rebar Shape" của người dùng:
    ///
    ///  T00          = thanh thẳng (straight bar)
    ///  T02–T07      = thanh 1 đầu bẻ (L-hook, different angles)
    ///  T11/T11a     = thanh chữ L hook 90° (dowel / starter bar)
    ///  T12–T17      = các biến thể hook 1 đầu
    ///  T20–T29      = U-bar, Z-bar, S-bar, bar với 2 đầu bẻ
    ///  T31–T38      = bar nhiều đoạn, cong phức tạp
    ///  T41–T49      = bar với nhiều chân (multi-leg)
    ///  T51          = đai kín hình chữ nhật, góc bo, hook 90° (rectangular closed stirrup)
    ///  T63/T67/T68  = đai hở, móng chữ U
    ///  T75          = vòng kín hình tròn (circular closed stirrup)
    ///  T80          = đai hình thoi / trụ bát giác
    /// </summary>
    public static class RebarShapeConfig
    {
        // ==================== THANH THẲNG ====================
        /// <summary>Thanh thẳng đứng — thép chủ cột tròn và vuông.</summary>
        public const string StraightMainBar = "JP_T00";

        // ==================== HOOK BARS (dowel / neo / chờ) ====================
        /// <summary>Thanh chữ L hook 90° — thép chờ (starter bar / dowel). Hook hướng phải.</summary>
        public const string LHook90Right  = "JP_T11";
        /// <summary>Thanh chữ L hook 90° — thép chờ (starter bar / dowel). Hook hướng trái (mirror).</summary>
        public const string LHook90Left   = "JP_T11a";
        /// <summary>Thanh bẻ 1 đầu 45° — neo chéo.</summary>
        public const string Hook45        = "JP_T02";
        /// <summary>Thanh bẻ 1 đầu 90° dạng ngắn — neo đầu cột.</summary>
        public const string HookShort90   = "JP_T03";
        /// <summary>Thanh với hook 135° (seismic hook) — tuỳ chọn đầu nối đai.</summary>
        public const string HookSeismic135 = "JP_T04";

        // ==================== U-BAR / Z-BAR / S-BAR ====================
        /// <summary>U-bar (cả 2 đầu bẻ 90°, cùng chiều) — thép treo, stirrup mở.</summary>
        public const string UBar          = "JP_T20";
        /// <summary>Z-bar (2 đầu bẻ ngược chiều) — thép nối chéo.</summary>
        public const string ZBar          = "JP_T21";
        /// <summary>S-bar — cốt thép hình chữ S.</summary>
        public const string SBar          = "JP_T22";

        // ==================== ĐAI STIRRUP ====================
        /// <summary>Đai kín hình chữ nhật (closed-loop rectangular, rounded corners, 90° hook pair) — thép đai cột vuông.</summary>
        public const string RectangularStirrup = "JP_T51";
        /// <summary>Vòng kín hình tròn (closed-loop circular) — thép đai cột tròn.</summary>
        public const string CircularStirrup    = "JP_T75";
        /// <summary>Đai hình thoi / bát giác — cột tiết diện đa giác.</summary>
        public const string DiamondStirrup     = "JP_T80";
        /// <summary>Đai hở chữ U (open stirrup / link) — cốt thép dầm móng 1 phía.</summary>
        public const string OpenStirrupU       = "JP_T63";
        /// <summary>Đai hở 1 nhánh thẳng đứng (hairpin) — link bổ sung cột vuông.</summary>
        public const string HairpinLink        = "JP_T67";
        /// <summary>Thanh 02 (C-link / Crosstie có 2 đầu uốn móc 180° Hook 180) — crosslink cho nhóm thép chủ cột.</summary>
        public const string CrossLink          = "JP_T02";

        // ==================== SHORTHAND HELPERS ====================
        /// <summary>Thép chờ mặc định (starter bar, dowel xuống móng) — alias cho LHook90Right.</summary>
        public const string StarterBar = LHook90Right;
        /// <summary>Neo đầu cột (top anchor) — alias cho HookShort90.</summary>
        public const string TopAnchor  = HookShort90;
    }
}
