namespace SophieDesktopPet;

internal enum PetState
{
    Idle,
    RunningRight,
    RunningLeft,
    Waving,
    Jumping,
    Failed,
    Waiting,
    Working,
    Reviewing,
    Sitting,
    Eating,
    Sleeping,
    Gardening,
    Reading
}

internal static class LifeAnimations
{
    public static AnimationDefinition For(PetState state) => state switch
    {
        PetState.Idle => new(0, 4, true),
        PetState.RunningRight => new(1, 4, true),
        PetState.RunningLeft => new(2, 4, true),
        PetState.Sitting => new(3, 4, false),
        PetState.Eating => new(4, 4, true),
        PetState.Sleeping => new(5, 4, true),
        PetState.Gardening => new(6, 4, true),
        PetState.Reading => new(7, 4, true),
        PetState.Working => new(7, 4, true),
        PetState.Reviewing => new(7, 4, true),
        _ => new(0, 4, true)
    };
}

internal readonly record struct AnimationDefinition(int Row, int Frames, bool Loops);

internal static class Animations
{
    public static AnimationDefinition For(PetState state) => state switch
    {
        PetState.Idle => new(0, 6, true),
        PetState.RunningRight => new(1, 8, true),
        PetState.RunningLeft => new(2, 8, true),
        PetState.Waving => new(3, 4, false),
        PetState.Jumping => new(4, 5, false),
        PetState.Failed => new(5, 8, false),
        PetState.Waiting => new(6, 6, true),
        PetState.Working => new(7, 6, true),
        PetState.Reviewing => new(8, 6, true),
        _ => new(0, 6, true)
    };
}
