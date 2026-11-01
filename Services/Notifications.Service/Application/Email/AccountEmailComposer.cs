using System.Globalization;
using Notifications.Service.Application.Events;
using Notifications.Service.Models;

namespace Notifications.Service.Application.Email;

public sealed record AccountEmailDraft(AccountEmailKind Kind, string Subject, string Text, DateTime? LinkExpiresAt);

// The text of the account emails. The links open pages of the web app, which call the API with the token.
public static class AccountEmailComposer
{
    public const string VerifyEmailPath = "verificar-email";
    public const string ResetPasswordPath = "redefinir-palavra-passe";
    public const string ForgotPasswordPath = "recuperar-palavra-passe";
    public const string LoginPath = "entrar";

    private static readonly CultureInfo Portugal = CultureInfo.GetCultureInfo("pt-PT");
    // Times are shown as the users see them; UTC if the container has no time zone data.
    private static readonly TimeZoneInfo Lisbon =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Lisbon", out var zone) ? zone : TimeZoneInfo.Utc;

    public static AccountEmailDraft EmailVerification(AccountTokenMessage message, Uri appBaseUrl) => new(
        AccountEmailKind.EmailVerification,
        "Confirm your email address",
        $"Welcome to CropConnect, {message.Username}!\n\n" +
        "Confirm your email address to finish creating your account:\n\n" +
        $"{Link(appBaseUrl, VerifyEmailPath, message.Token)}\n\n" +
        $"The link works once and expires on {Format(message.ExpiresAt)}. " +
        "If you did not create this account, you can ignore this email.",
        message.ExpiresAt);

    public static AccountEmailDraft PasswordReset(AccountTokenMessage message, Uri appBaseUrl) => new(
        AccountEmailKind.PasswordReset,
        "Reset your password",
        $"Hello {message.Username},\n\n" +
        "We received a request to reset the password of your CropConnect account. Choose a new one here:\n\n" +
        $"{Link(appBaseUrl, ResetPasswordPath, message.Token)}\n\n" +
        $"The link works once and expires on {Format(message.ExpiresAt)}. " +
        "If you did not ask for it, ignore this email: your password stays the same.",
        message.ExpiresAt);

    public static AccountEmailDraft Invitation(AccountTokenMessage message, Uri appBaseUrl) => new(
        AccountEmailKind.Invitation,
        "Your CropConnect account is ready",
        $"Hello {message.Username},\n\n" +
        $"An administrator created a CropConnect account for you, with the {RoleName(message.Role)} profile. " +
        "Choose your password to start using it:\n\n" +
        $"{Link(appBaseUrl, ResetPasswordPath, message.Token)}\n\n" +
        $"The link works once and expires on {Format(message.ExpiresAt)}. After that, use " +
        $"\"Forgot password\" on {new Uri(appBaseUrl, ForgotPasswordPath)}.",
        message.ExpiresAt);

    public static AccountEmailDraft Welcome(AccountMessage message, Uri appBaseUrl) => new(
        AccountEmailKind.Welcome,
        "Your email is confirmed",
        $"Hello {message.Username},\n\n" +
        $"Your email is confirmed and your account is ready. {WhatNext(message.Role)}\n\n" +
        $"{new Uri(appBaseUrl, LoginPath)}",
        null);

    public static AccountEmailDraft PasswordChanged(AccountMessage message, Uri appBaseUrl) => new(
        AccountEmailKind.PasswordChanged,
        "Your password was changed",
        $"Hello {message.Username},\n\n" +
        $"The password of your CropConnect account was changed on {Format(message.OccurredAt)}. " +
        "Sessions opened with the old password were ended.\n\n" +
        "If it wasn't you, reset your password right away:\n\n" +
        $"{new Uri(appBaseUrl, ForgotPasswordPath)}",
        null);

    public static AccountEmailDraft Deactivated(AccountMessage message) => new(
        AccountEmailKind.AccountDeactivated,
        "Your account was deactivated",
        $"Hello {message.Username},\n\n" +
        "An administrator deactivated your CropConnect account, so you can no longer log in. " +
        "If you think this is a mistake, contact the platform's administrator.",
        null);

    public static AccountEmailDraft Reactivated(AccountMessage message, Uri appBaseUrl) => new(
        AccountEmailKind.AccountReactivated,
        "Your account was reactivated",
        $"Hello {message.Username},\n\n" +
        "An administrator reactivated your CropConnect account. You can log in again:\n\n" +
        $"{new Uri(appBaseUrl, LoginPath)}",
        null);

    private static string Link(Uri appBaseUrl, string path, string token) =>
        new Uri(appBaseUrl, $"{path}?token={Uri.EscapeDataString(token)}").ToString();

    private static string Format(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Lisbon).ToString("dd/MM/yyyy HH:mm", Portugal);

    private static string RoleName(string role) => role.ToLowerInvariant() switch
    {
        "manager" => "farm manager",
        "admin" => "administrator",
        _ => "client"
    };

    private static string WhatNext(string role) => role.ToLowerInvariant() switch
    {
        "manager" => "Log in to register your farm, add your products and start receiving orders.",
        "admin" => "Log in to manage the platform.",
        _ => "Log in to explore the local farms and reserve your first products."
    };
}
