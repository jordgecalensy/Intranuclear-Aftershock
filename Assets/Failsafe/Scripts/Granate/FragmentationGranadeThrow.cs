public class FragmentationGranadeThrow : Thrower, IUsableGranade
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

