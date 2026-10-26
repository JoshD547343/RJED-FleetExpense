// Models/ExpenseCreateViewModel.cs
public class ExpenseCreateViewModel
{
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public string? PlateNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<IFormFile>? ReceiptFiles { get; set; }
}