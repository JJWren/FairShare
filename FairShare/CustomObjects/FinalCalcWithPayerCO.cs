namespace FairShare.CustomObjects
{
    public class FinalCalcWithPayerCO(string payer, int finalAmount)
    {
        public string Payer { get; set; } = payer;

        public int FinalAmount { get; set; } = finalAmount;
    }
}
