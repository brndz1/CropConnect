namespace Notifications.Service.Models;

public enum EmailStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,

    // Not sent on purpose, e.g. the recipient's account is inactive.
    Skipped = 4
}
