namespace OyasumiGray.Core;

public sealed class ColorMatrix
{
    private readonly float[] _values;
    public ReadOnlySpan<float> Values => _values;

    private ColorMatrix(float[] values) => _values = values;

    public static ColorMatrix Identity { get; } = ForRatio(0);
    public bool IsIdentity => NearlyEquals(Identity);

    public static ColorMatrix ForRatio(double ratio)
    {
        if (!double.IsFinite(ratio) || ratio is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(ratio));
        float p = (float)ratio;
        float[] matrix = new float[25];
        matrix[0] = 1 - p + p * .2126f;
        matrix[1] = p * .2126f;
        matrix[2] = p * .2126f;
        matrix[5] = p * .7152f;
        matrix[6] = 1 - p + p * .7152f;
        matrix[7] = p * .7152f;
        matrix[10] = p * .0722f;
        matrix[11] = p * .0722f;
        matrix[12] = 1 - p + p * .0722f;
        matrix[18] = 1;
        matrix[24] = 1;
        return new(matrix);
    }

    public static ColorMatrix FromArray(float[] values)
    {
        if (values.Length != 25 || values.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("The effect must contain 25 finite values.", nameof(values));
        return new((float[])values.Clone());
    }

    public float[] ToArray() => (float[])_values.Clone();

    public bool NearlyEquals(ColorMatrix other)
    {
        for (int i = 0; i < 25; i++)
            if (Math.Abs(_values[i] - other._values[i]) > 1e-4f) return false;
        return true;
    }
}
