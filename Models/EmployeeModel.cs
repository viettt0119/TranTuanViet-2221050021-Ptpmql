using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class EmployeeModel
    {
        [Key]
        public string EmpId { get; set; } = default!;
        public string FullName { get; set; } = default!;

        public DateTime HireDate { get; set; } = DateTime.Now; 
    }
}