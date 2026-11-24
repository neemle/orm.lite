namespace Neemle.Orm.Lite.Tests.Behaviour;

// Declared in value order (Low < Mid < High) and NOT in alphabetical order (High < Low < Mid), so that a text
// ranking of the stored names and a ranking by value disagree on High against Low and against Mid (Low and Mid agree).
public enum EnumTier { Low = 0, Mid = 1, High = 2 }
