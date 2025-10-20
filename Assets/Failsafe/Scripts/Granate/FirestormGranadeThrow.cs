public class FirestormGranadeThrow : Thrower, IUsableGranade
{
    void IUsableGranade.Use()
    {
        Throw(false);
    }
    void IUsableGranade.AltUse()
    {
        Throw(true);
    }
}

