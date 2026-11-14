using Microsoft.AspNetCore.Identity;

namespace CoachFlow.Api.Accounts;

// Identity liefert englische Meldungen. Hier stehen die, die Nutzende tatsächlich sehen können.
public class GermanIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Ein unbekannter Fehler ist aufgetreten." };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Der Link ist ungültig oder abgelaufen." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Die E-Mail-Adresse ist ungültig." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Die E-Mail-Adresse ist ungültig." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Für diese E-Mail-Adresse gibt es bereits ein Konto." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Für diese E-Mail-Adresse gibt es bereits ein Konto." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Das Passwort muss mindestens {length} Zeichen lang sein." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Das Passwort ist falsch." };
}
