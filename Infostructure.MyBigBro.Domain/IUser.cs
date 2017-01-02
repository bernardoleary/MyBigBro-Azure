namespace Infostructure.MyBigBro.Domain
{
    public interface IUser
    {
        int Id { get; set; }
        string UserName { get; set; }
        string Email { get; set; }
        string Password{ get; set; }
        bool IsAdmin { get; set; }
    }
} 