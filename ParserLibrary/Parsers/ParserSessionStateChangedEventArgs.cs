namespace ParserLibrary.Parsers;

public sealed class ParserSessionStateChangedEventArgs : EventArgs
{
    public ParserSessionStateChangedEventArgs(ParserSessionState oldState, ParserSessionState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public ParserSessionState OldState { get; }
    public ParserSessionState NewState { get; }
}
