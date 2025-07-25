namespace ChatBox.API.Payload.Response.SecurityServiceResponse
{
    public class PIIEntity
    {
        public string Type { get; set; } // email, phone, ssn, credit_card, etc.
        public string Value { get; set; }
        public string MaskedValue { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public double Confidence { get; set; }
        public string Context { get; set; }
    }
}
