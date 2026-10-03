using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge.Registration;

public sealed record ForgeModuleAdmission(string MachineId, IReadOnlyList<string> ModuleTypes);
internal sealed class ModuleAdmissionCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Owner, string Machine), string[]> _rules = new();
    internal SubmitResult Submit(string owner, IReadOnlyList<ForgeModuleAdmission> rules, Func<bool>? ensureAdapter = null)
    {
        if (rules == null || rules.Count == 0) return new(SubmitStatus.Invalid, "Admission batch is empty");
        var frozen = new Dictionary<string, string[]>(StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.MachineId) || rule.ModuleTypes == null ||
                    rule.ModuleTypes.Count == 0 || !frozen.TryAdd(rule.MachineId, rule.ModuleTypes.ToArray()) ||
                    rule.ModuleTypes.Any(t => !OwnedCallbacks<bool, bool>.ValidId(owner, t)) ||
                    rule.ModuleTypes.Distinct(StringComparer.Ordinal).Count() != rule.ModuleTypes.Count)
                    return new(SubmitStatus.Invalid, "Each machine needs unique module types belonging to the owner");
                if (_rules.TryGetValue((owner, rule.MachineId), out var old) &&
                    !old.SequenceEqual(frozen[rule.MachineId], StringComparer.Ordinal))
                    return new(SubmitStatus.Conflict, "Owner already contributed different admission types");
            }
        }
        if (ensureAdapter != null && !ensureAdapter()) return new(SubmitStatus.Invalid, "Module bay adapter unavailable");
        lock (_gate)
        {
            // Native adapter setup runs outside the lock and may re-enter this
            // catalog. Recheck the entire batch before publishing any rule.
            foreach (var (machine, types) in frozen)
                if (_rules.TryGetValue((owner, machine), out var old) &&
                    !old.SequenceEqual(types, StringComparer.Ordinal))
                    return new(SubmitStatus.Conflict, "Owner admission changed during adapter setup");
            bool added = false;
            foreach (var (machine, types) in frozen) added |= _rules.TryAdd((owner, machine), types);
            return new(added ? SubmitStatus.Accepted : SubmitStatus.AlreadyPresent, "Module admission staged");
        }
    }
    internal string[] Merge(string machine, IEnumerable<string> original)
    {
        lock (_gate) return original.Concat(
            _rules.Where(r => r.Key.Machine == machine).OrderBy(r => r.Key.Owner, StringComparer.Ordinal)
                .SelectMany(r => r.Value)).Distinct(StringComparer.Ordinal).ToArray();
    }
    internal bool RemoveOwner(string owner)
    {
        lock (_gate)
        {
            foreach (var key in _rules.Keys.Where(k => k.Owner == owner).ToArray()) _rules.Remove(key);
            return _rules.Count == 0;
        }
    }
}
