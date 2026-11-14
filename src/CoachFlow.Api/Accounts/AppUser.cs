using Microsoft.AspNetCore.Identity;

namespace CoachFlow.Api.Accounts;

public class AppUser : IdentityUser
{
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;
}
