using System.Collections.Generic;
using System.Linq;
using Yachu.Server.Util;

namespace Yachu.Server.Gameplay {
public class YachuScoreStorage {
    private readonly Dictionary<YachuScoreTypeEnum, int> _map = new(YachuScoreType.Types.Count);

    public void Reset() {
        _map.Clear();
        Update();
    }

    public int? this[YachuScoreTypeEnum type] {
        get {
            if (_map.TryGetValue(type, out var score)) {
                return score;
            }
            return null;
        }
        set {
            if(value != null)
                _map[type] = value.Value;
            else {
                _map.Remove(type);
            }

            Update();
        }
    }

    private static readonly int SubtotalCount = YachuScoreType.Subtotal.Count;
    public static readonly int SubtotalBonus = Constants.SubtotalBonusScore;
    public int Total { get; private set; } = 0;
    public int Subtotal { get; private set; } = 0;
    public int Bonus { get; private set; } = 0;
    public bool SubtotalFilled { get; private set; } = false;
    public bool BonusDetermined => Subtotal >= SubtotalBonus || SubtotalFilled;
    private void Update() {
        var subtotalTypes = _map.Where(it => it.Key.Of().IsSubtotal).ToList();
        Subtotal = subtotalTypes.Sum(it => it.Value);
        SubtotalFilled = subtotalTypes.Count >= SubtotalCount;
        Bonus = Subtotal >= 63 ? 35 : 0;
        Total = _map.Sum(it => it.Value) + Bonus;

    }
}
}