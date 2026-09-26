namespace OyasumiGray.Core;

public interface IDisplayEffect
{
    ColorMatrix Read();
    void Write(ColorMatrix matrix);
}

public enum EffectState { Idle, Applied, Conflict, Fault }

public sealed class EffectController(IDisplayEffect effect)
{
    private ColorMatrix? _lastWritten;
    public EffectState State { get; private set; }
    public string? Error { get; private set; }
    public bool HasOwnedEffect => _lastWritten is not null;

    public void CheckInitial()
    {
        try
        {
            if (!effect.Read().IsIdentity) State = EffectState.Conflict;
        }
        catch (Exception ex) { Fail(ex); }
    }

    public void Apply(double ratio)
    {
        if (State is EffectState.Conflict or EffectState.Fault) return;
        if (ratio <= 0) { Release(); return; }
        try
        {
            var current = effect.Read();
            if (_lastWritten is null ? !current.IsIdentity : !current.NearlyEquals(_lastWritten))
            {
                _lastWritten = null;
                State = EffectState.Conflict;
                return;
            }
            var target = ColorMatrix.ForRatio(ratio);
            if (!current.NearlyEquals(target))
            {
                effect.Write(target);
                _lastWritten = target;
            }
            State = EffectState.Applied;
            Error = null;
        }
        catch (Exception ex) { Fail(ex); }
    }

    public void Release()
    {
        if (_lastWritten is null || State == EffectState.Conflict) return;
        try
        {
            var current = effect.Read();
            if (!current.IsIdentity && !current.NearlyEquals(_lastWritten))
            {
                _lastWritten = null;
                State = EffectState.Conflict;
                return;
            }
            if (!current.IsIdentity) effect.Write(ColorMatrix.Identity);
            _lastWritten = null;
            State = EffectState.Idle;
            Error = null;
        }
        catch (Exception ex) { Fail(ex); }
    }

    public void Retry()
    {
        if (State is not (EffectState.Conflict or EffectState.Fault)) return;
        try
        {
            var current = effect.Read();
            if (!current.IsIdentity) { State = EffectState.Conflict; return; }
            _lastWritten = null;
            State = EffectState.Idle;
            Error = null;
        }
        catch (Exception ex) { Fail(ex); }
    }

    public void ReconcileAfterDisplayChange()
    {
        if (State is EffectState.Conflict or EffectState.Fault) return;
        try
        {
            var current = effect.Read();
            if (current.IsIdentity)
            {
                _lastWritten = null;
                State = EffectState.Idle;
            }
            else if (_lastWritten is null || !current.NearlyEquals(_lastWritten))
            {
                _lastWritten = null;
                State = EffectState.Conflict;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    public void ForceClear()
    {
        try
        {
            effect.Write(ColorMatrix.Identity);
            _lastWritten = null;
            State = EffectState.Idle;
            Error = null;
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
        State = EffectState.Fault;
        Error = $"{ex.GetType().Name}: {ex.Message}";
    }
}
