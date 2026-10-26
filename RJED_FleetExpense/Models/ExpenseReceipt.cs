namespace RJED_FleetExpense.Models
{
    public class ExpenseReceipt
    {
        public int Id { get; set; }

        public int ExpenseId { get; set; }
        public Expense Expense { get; set; }

        public string FilePath { get; set; }
    }
}