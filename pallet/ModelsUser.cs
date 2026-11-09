namespace PalletApp.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Login { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsAuthenticated { get; set; }
    }
}