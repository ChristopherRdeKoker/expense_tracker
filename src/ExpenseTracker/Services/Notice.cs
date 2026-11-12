namespace ExpenseTracker.Services;

public sealed record Notice(string Message, NoticeTone Tone, bool CanUndo = false, bool Sticky = false);
