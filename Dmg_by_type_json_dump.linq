<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
</Query>

public class JsonOutputGenerator 
{
    public static string GenerateJson(CombatAnalysis analysis)
    {
        var output = new
        {
            combatSummary = new
            {
                totalDamageByType = analysis.DamageByType,
                totalMisses = analysis.TotalMisses,
                totalFailedCasts = analysis.TotalFailedCasts
            },
            abilityDetails = analysis.AbilityStats.Select(kvp => new
            {
                name = kvp.Key,
                stats = new
                {
                    totalDamage = kvp.Value.TotalDamage,
                    hits = kvp.Value.Hits,
                    crits = kvp.Value.Crits,
                    averageDamage = Math.Round(kvp.Value.AverageDamage, 2),
                    critRate = Math.Round(kvp.Value.CritRate * 100, 2)
                }
            })
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
    }
}

public class AbilityStats
{
    public int TotalDamage { get; private set; }
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public double AverageDamage => Hits > 0 ? TotalDamage / (double)Hits : 0;
    public double CritRate => Hits > 0 ? Crits / (double)Hits : 0;

    public void AddDamage(DamageEvent evt)
    {
        TotalDamage += evt.Amount;
        Hits++;
        if (evt.IsCritical) Crits++;
    }
}

public class CombatLog
{
    public List<LogMessage> Messages { get; set; } = new();
}

public class LogMessage 
{
    public string Message { get; set; }
    public int ChannelType { get; set; }
    public int DirectionalFilter { get; set; }
    public int CombatLogFilter { get; set; }
    public int CombatLogPlayerFilter { get; set; }
}

public class CombatAnalysis
{
    public Dictionary<string, int> DamageByType { get; } = new();
    public Dictionary<string, AbilityStats> AbilityStats { get; } = new();
    public int TotalMisses { get; private set; }
    public int TotalFailedCasts { get; private set; }

    public void AddDamageEvent(DamageEvent evt)
    {
        if (!DamageByType.ContainsKey(evt.DamageType))
            DamageByType[evt.DamageType] = 0;
        DamageByType[evt.DamageType] += evt.Amount;

        if (!AbilityStats.ContainsKey(evt.Ability))
            AbilityStats[evt.Ability] = new AbilityStats();
        AbilityStats[evt.Ability].AddDamage(evt);
    }

    public void AddMiss(string message) => TotalMisses++;
    public void AddFailedCast(string message) => TotalFailedCasts++;
}

public class CombatAnalyzer
{
    private readonly CombatLog _log;
    public CombatAnalysis Analysis { get; }

    public CombatAnalyzer(CombatLog log)
    {
        _log = log;
        Analysis = AnalyzeCombatLog();
    }

    private CombatAnalysis AnalyzeCombatLog()
    {
        var analysis = new CombatAnalysis();
        
        foreach (var msg in _log.Messages)
        {
            if (TryParseDamageEvent(msg.Message, out var dmg))
            {
                analysis.AddDamageEvent(dmg);
            }
            else if (msg.Message.Contains("Failed ability cast"))
            {
                analysis.AddFailedCast(msg.Message);
            }
            else if (msg.Message.Contains("missed"))
            {
                analysis.AddMiss(msg.Message);
            }
        }
        
        return analysis;
    }

    public DetailedAbilityStats GetDetailedAbilityStats()
    {
        var stats = new DetailedAbilityStats();
        var damageEvents = _log.Messages
            .Where(m => m.Message.Contains(" dealt "))
            .Select(m => ParseDamageEvent(m.Message))
            .Where(d => d != null);

        foreach (var evt in damageEvents)
        {
            stats.AddDamageEvent(evt);
        }
        
        return stats;
    }

    public FailureAnalysis GetFailureAnalysis()
    {
        var analysis = new FailureAnalysis();
        var failures = _log.Messages
            .Where(m => m.Message.Contains("Failed ability cast"))
            .Select(m => m.Message.Split(":")[1].Trim());
            
        foreach (var reason in failures)
        {
            analysis.AddFailure(reason);
        }
        
        return analysis;
    }

    private bool TryParseDamageEvent(string message, out DamageEvent dmg)
    {
        dmg = null;
        if (!message.Contains(" dealt ")) return false;
        
        dmg = ParseDamageEvent(message);
        return dmg != null;
    }

    private DamageEvent ParseDamageEvent(string message)
    {
        var match = Regex.Match(message, @"(?<source>.+) dealt (?<amount>\d+) (?<type>\w+) damage to (?<target>.+) with (?<ability>[^.]+)");
        if (!match.Success) return null;

        return new DamageEvent
        {
            Source = match.Groups["source"].Value,
            Amount = int.Parse(match.Groups["amount"].Value),
            DamageType = match.Groups["type"].Value,
            Target = match.Groups["target"].Value,
            Ability = match.Groups["ability"].Value.Trim(),
            IsCritical = message.Contains("(Critical)"),
            IsMitigated = message.Contains("mitigated")
        };
    }
}

public class DetailedAbilityStats
{
    public Dictionary<string, AbilityMetrics> AbilityStats { get; } = new();
    
    public void AddDamageEvent(DamageEvent evt)
    {
        if (!AbilityStats.ContainsKey(evt.Ability))
            AbilityStats[evt.Ability] = new AbilityMetrics();
            
        var metrics = AbilityStats[evt.Ability];
        metrics.AddDamage(evt);
    }
}

public class AbilityMetrics
{
    public int TotalDamage { get; private set; }
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public List<int> DamageValues { get; } = new();
    
    public double AverageDamage => Hits > 0 ? TotalDamage / (double)Hits : 0;
    public double CritRate => Hits > 0 ? Crits / (double)Hits : 0;
    public int MinDamage => DamageValues.Any() ? DamageValues.Min() : 0;
    public int MaxDamage => DamageValues.Any() ? DamageValues.Max() : 0;
    
    public void AddDamage(DamageEvent evt)
    {
        TotalDamage += evt.Amount;
        Hits++;
        if (evt.IsCritical) Crits++;
        DamageValues.Add(evt.Amount);
    }
}

public class FailureAnalysis
{
    public Dictionary<string, int> FailureReasons { get; } = new();
    
    public void AddFailure(string reason)
    {
        if (!FailureReasons.ContainsKey(reason))
            FailureReasons[reason] = 0;
        FailureReasons[reason]++;
    }
}

public class DamageEvent
{
    public string Source { get; set; }
    public string Target { get; set; }
    public string Ability { get; set; }
    public int Amount { get; set; }
    public string DamageType { get; set; }
    public bool IsCritical { get; set; }
    public bool IsMitigated { get; set; }
}

void Main()
{
    Console.WriteLine("Reading combat log...");
    var json = File.ReadAllText(@"C:\Users\Glennwiz\AppData\Local\Temp\Visionary Realms\Pantheon\Whispering Lands (1)\Mxx\Combat");
    Console.WriteLine($"Raw JSON length: {json.Length}");
    
    var combatData = JsonSerializer.Deserialize<CombatLog>(json);
    var analyzer = new CombatAnalyzer(combatData);
    
    var jsonOutput = JsonOutputGenerator.GenerateJson(analyzer.Analysis);
    Console.WriteLine("\nAnalysis JSON:");
    Console.WriteLine(jsonOutput);
    
    analyzer.Analysis.Dump("Combat Analysis");
    analyzer.GetDetailedAbilityStats().Dump("Detailed Ability Stats");
    analyzer.GetFailureAnalysis().Dump("Failure Analysis");
}