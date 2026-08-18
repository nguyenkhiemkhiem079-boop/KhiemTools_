using System;
using System.Collections.Generic;

namespace KhimTools.SheetExport.Models
{
    public class NamingTemplate
    {
        public string Name { get; set; } = "Chuẩn mặc định";
        public string Expression { get; set; } = "{SheetNumber}_{SheetName}";
        public string RegexPattern { get; set; } = @"^[A-Za-z0-9_\-\.\s]+$";
        public bool IsDefault { get; set; }

        public static List<NamingTemplate> GetBuiltInTemplates()
        {
            return new List<NamingTemplate>
            {
                new NamingTemplate
                {
                    Name = "Mặc định (SheetNumber - SheetName)",
                    Expression = "{SheetNumber} - {SheetName}",
                    RegexPattern = @"^[A-Za-z0-9_\-\.\s]+$",
                    IsDefault = true
                },
                new NamingTemplate
                {
                    Name = "ISO / Standard (Project_Sheet_Rev)",
                    Expression = "{ProjectCode}_{SheetNumber}_Rev{Revision}",
                    RegexPattern = @"^[A-Za-z0-9]+_[A-Za-z0-9\-]+_Rev[A-Za-z0-9]+$",
                    IsDefault = false
                },
                new NamingTemplate
                {
                    Name = "Chuẩn Phát Hành (Date_Project_Sheet)",
                    Expression = "{Date}_{ProjectCode}_{SheetNumber}_{SheetName}",
                    RegexPattern = @"^\d{8}_[A-Za-z0-9]+_[A-Za-z0-9\-]+_.*$",
                    IsDefault = false
                }
            };
        }
    }
}
