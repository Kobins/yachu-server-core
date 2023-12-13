using System.Collections.Generic;
using System.Linq;

namespace Yachu.Server.Gameplay {
public delegate int ScoreCalculator(List<int> dices);

public enum YachuScoreTypeEnum {
    Aces = 0,
    Deuces,
    Threes,
    Fours,
    Fives,
    Sixes,
    
    Choice,
    FourOfAKind,
    FullHouse,
    SmallStraight,
    LargeStraight,
    Yachu,
    
    TypeCount
    
}
public class YachuScoreType {

    public static readonly List<YachuScoreType> Types = new List<YachuScoreType>(){
        new (YachuScoreTypeEnum.Aces, "Aces"  , "눈이 1인 주사위 눈의 합", true, false, (dices) => dices.Where(it => it == 1).Sum()),
        new (YachuScoreTypeEnum.Deuces, "Deuces", "눈이 2인 주사위 눈의 합", true, false, (dices) => dices.Where(it => it == 2).Sum()),
        new (YachuScoreTypeEnum.Threes, "Threes", "눈이 3인 주사위 눈의 합", true, false, (dices) => dices.Where(it => it == 3).Sum()),
        new (YachuScoreTypeEnum.Fours, "Fours" , "눈이 4인 주사위 눈의 합", true, false, (dices) => dices.Where(it => it == 4).Sum()),
        new (YachuScoreTypeEnum.Fives, "Fives" , "눈이 5인 주사위 눈의 합", true, false, (dices) => dices.Where(it => it == 5).Sum()),
        new (YachuScoreTypeEnum.Sixes, "Sixes" , "눈이 6인 주사위 눈의 합", true, false, (dices) => dices.Where(it => it == 6).Sum()),
        new (YachuScoreTypeEnum.Choice, "Choice", "주사위 눈의 합", false, false, (dices) => dices.Sum()),
        new (YachuScoreTypeEnum.FourOfAKind, "Four of a Kind", "같은 눈의 주사위가 4개일 때 총합", false, true, (dices) => {
            var map = dices.GroupBy(it => it);
            if (map.Any(it => it.Count() >= 4)) {
                return dices.Sum();
            }
            return 0;
        }),
        new (YachuScoreTypeEnum.FullHouse, "Full House", "같은 주사위 눈이 각각 3개, 2개일 때 총합", false, true, (dices) => {
            var groupBy = dices.GroupBy(it => it);
            var map = new Dictionary<int, int>();
            foreach (var group in groupBy) {
                int size = 0;
                foreach (var value in group) {
                    ++size;
                }

                map[group.Key] = size;
            }

            if (map.Any(it => it.Value == 3) && map.Any(it => it.Value == 2) 
                || map.Any(it => it.Value == 5)
            ) {
                return dices.Sum();
            }

            return 0;
        }),
        new (YachuScoreTypeEnum.SmallStraight, "Small Straight", "주사위 눈이 4개 연속될 때 고정 15점", false, true, (dices) => {
            var numbers = dices.Distinct().ToList();
            numbers.Sort();
            var count = 0;
            for (int index = 0; index < numbers.Count; index++) {
                if(index == 0) continue;
                var number = numbers[index];
                if (number - numbers[index - 1] != 1) {
                    count = 0;
                    continue;
                }

                count++;
                if (count >= 3) {
                    return 15;
                }
            }
            return 0;
        }),
        new (YachuScoreTypeEnum.LargeStraight, "Large Straight", "주사위 눈이 5개 연속될 때 고정 30점", false, true, (dices) => {
            var numbers = dices.Distinct().ToList();
            numbers.Sort();
            var count = 0;
            for (int index = 0; index < numbers.Count; index++) {
                if(index == 0) continue;
                var number = numbers[index];
                if (number - numbers[index - 1] != 1) {
                    count = 0;
                    continue;
                }

                count++;
                if (count >= 4) {
                    return 30;
                }
            }
            return 0;
        }),
        new (YachuScoreTypeEnum.Yachu, "Yachu", "같은 주사위 눈이 5개일 때 고정 50점", false, true, (dices) => {
            return dices.Distinct().Count() == 1 ? 50 : 0;
        }),
    };

    public static readonly List<YachuScoreType> Subtotal = Types.Where(it => it.IsSubtotal).ToList();
    public static readonly List<YachuScoreType> Special = Types.Where(it => it.IsSpecial).ToList();

    private static readonly Dictionary<YachuScoreTypeEnum, YachuScoreType> Map = Types.ToDictionary(it => it.TypeEnum);

    public static YachuScoreType Of(YachuScoreTypeEnum typeEnum) {
        return Map[typeEnum];
    }

    public YachuScoreTypeEnum TypeEnum { get; }
    public string Text { get; }
    public string Description { get; }
    public bool IsSubtotal { get; }
    public bool IsSpecial { get; }
    
    public ScoreCalculator Calculator { get; }

    private YachuScoreType(YachuScoreTypeEnum typeEnum, string text, string description, bool isSubtotal, bool isSpecial, ScoreCalculator calculator) {
        TypeEnum = typeEnum;
        Text = text;
        Description = description;
        IsSubtotal = isSubtotal;
        IsSpecial = isSpecial;
        Calculator = calculator;
    }
}
public static class YachuScoreTypeEnumUtil {
    public static YachuScoreType Of(this YachuScoreTypeEnum typeEnum) {
        return YachuScoreType.Of(typeEnum);
    }
}
}