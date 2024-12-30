<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
</Query>


void Main()
{
    var json = File.ReadAllText(@"C:\Users\Glennwiz\AppData\Local\Temp\Visionary Realms\Pantheon\Whispering Lands (1)\Mxx\Combat");
    var data = JsonSerializer.Deserialize<CombatLog>(json);
    
    var weapons = data.Messages
        .Where(m => m.Message.Contains(" with "))
        .Select(m => m.Message.Split(" with ").Last().Split(".").First().Trim())
        .Distinct()
        .OrderBy(w => w);
        
    weapons.Dump("Weapons Used");
}

public class CombatLog
{
    public List<CombatMessage> Messages { get; set; }
}

public class CombatMessage
{
    public string Message { get; set; }
    public int ChannelType { get; set; }
}