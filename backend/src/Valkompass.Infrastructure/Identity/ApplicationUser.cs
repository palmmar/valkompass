using Microsoft.AspNetCore.Identity;

namespace Valkompass.Infrastructure.Identity;

/// <summary>Adminanvändare. Inga publika användarkonton finns — endast redaktörer/admins.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
}
