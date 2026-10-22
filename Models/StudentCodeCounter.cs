using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Models;

// Mỗi nhóm năm/hệ/ngành có một bộ đếm riêng.
[PrimaryKey(nameof(AdmissionYear), nameof(TrainingSystemCode), nameof(MajorCode))]
[Table("StudentCodeCounters")]
public class StudentCodeCounter
{
    [Range(2000, 2099)]
    public int AdmissionYear { get; set; }
    [Required, StringLength(2)]
    public string TrainingSystemCode { get; set; } = string.Empty;
    [Required, StringLength(2)]
    public string MajorCode { get; set; } = string.Empty;
    [Range(0, 9999)]
    public int LastNumber { get; set; }
}
