// 攻撃QTE(プレイヤーのこうげき) (Pygame 版の ui/qte.py)
//
//   AttackQTE  … 判定のしくみ。決まったキーを、決まった順番で、時間内に押せたら成功
//   DrawQte()  … 画面の真ん中に「押す順番」と残り時間を表示する
//
// ルール
//   ・A → S → D → F の順に押す
//   ・受付時間(data/battle_rules.csv の「攻撃QTEの受付時間(秒)」)以内に全部押せたら成功
//   ・ちがうキー(ゲームで使うキーのうち、次に押すキー以外)を押す → その場で失敗
//   ・時間切れ → 失敗
using System;
using System.Collections.Generic;
using System.Linq;

namespace QteRpg.UI
{
    public sealed class AttackQTE
    {
        public static readonly string[] ATTACK_SEQUENCE = { "A", "S", "D", "F" };  // 押す順番

        // 判定に使うキー(これ以外のキーは押しても何も起きない)
        public static readonly Dictionary<GameKey, string> GAME_KEYS = new Dictionary<GameKey, string>
        {
            { GameKey.Q, "Q" }, { GameKey.W, "W" }, { GameKey.E, "E" },
            { GameKey.A, "A" }, { GameKey.S, "S" }, { GameKey.D, "D" }, { GameKey.F, "F" },
            { GameKey.Z, "Z" }, { GameKey.X, "X" }, { GameKey.C, "C" },
        };

        public readonly List<string> Sequence;
        public int Index;            // 次に押すキーの番号(押せた数)
        public readonly double TimeLimit;
        public double TimeLeft;
        public bool? Result;         // null = 受付中、true = 成功、false = 失敗
        public double Timer;

        /// <summary>timeLimit … 受付時間(秒)。省略すると data/battle_rules.csv の「攻撃QTEの受付時間(秒)」</summary>
        public AttackQTE(IEnumerable<string> sequence = null, double? timeLimit = null)
        {
            Sequence = (sequence ?? ATTACK_SEQUENCE).ToList();
            Index = 0;
            TimeLimit = timeLimit ?? GameData.ATTACK_QTE_TIME;
            TimeLeft = TimeLimit;
            Result = null;
            Timer = 0.0;
        }

        public void HandleEvent(GameEvent e)
        {
            if (Result != null || e.Type != GameEventType.KeyDown || !GAME_KEYS.ContainsKey(e.Key))
            {
                return;
            }
            if (GAME_KEYS[e.Key] == Sequence[Index])
            {
                Index++;
                if (Index >= Sequence.Count)
                {
                    Result = true;
                }
            }
            else
            {
                Result = false;  // ちがうキー
            }
        }

        public void Update(double dt)
        {
            Timer += dt;
            if (Result == null)
            {
                TimeLeft = Math.Max(0.0, TimeLeft - dt);
                if (TimeLeft <= 0)
                {
                    Result = false;  // 時間切れ
                }
            }
        }
    }

    public static class QteView
    {
        const int BOX = 20;  // キーの四角の大きさ
        const int GAP = 8;   // 四角どうしのすき間

        /// <summary>
        /// 画面の真ん中に QTE の指示を出す: [A][S][D][F] と残り時間のバー
        /// 押せたキーは金色、次に押すキーは白く点滅、まだのキーはグレー
        /// </summary>
        public static void DrawQte(Surface surface, AttackQTE qte, (int x, int y) center)
        {
            int count = qte.Sequence.Count;
            int rowW = count * BOX + (count - 1) * GAP;
            var panel = PgRect.Anchored(Math.Max(rowW + 24, 120), 66, Anchor.Center, center.x, center.y);
            Draw.Rect(surface, Settings.BLACK, panel, 0, 3);
            Draw.Rect(surface, Settings.WHITE, panel, 1, 3);

            Utils.DrawText(surface, "こうげき！", Settings.FONT_SMALL, Settings.LIGHT_GRAY, Anchor.Center, panel.CenterX, panel.Top + 11);
            bool blink = (int)(qte.Timer * 8) % 2 == 0;
            int left = panel.CenterX - rowW / 2;
            for (int i = 0; i < qte.Sequence.Count; i++)
            {
                string key = qte.Sequence[i];
                var box = new PgRect(left + i * (BOX + GAP), panel.Top + 24, BOX, BOX);
                Rgba color;
                if (i < qte.Index)
                {
                    color = Settings.GLOW;
                }
                else if (i == qte.Index)
                {
                    color = blink ? Settings.WHITE : Settings.LIGHT_GRAY;
                }
                else
                {
                    color = Settings.GRAY;
                }
                Draw.Rect(surface, color, box, i == qte.Index ? 2 : 1, 2);
                Utils.DrawText(surface, key, Settings.FONT_SMALL, color, Anchor.Center, box.CenterX, box.CenterY - 1);
            }

            // 残り時間のバー
            var bar = new PgRect(panel.Left + 8, panel.Bottom - 10, panel.Width - 16, 4);
            Draw.Rect(surface, Settings.GRAY, bar);
            int filled = Py.Round(bar.Width * qte.TimeLeft / qte.TimeLimit);
            if (filled > 0)
            {
                Draw.Rect(surface, Settings.WHITE, new PgRect(bar.Left, bar.Top, filled, bar.Height));
            }
        }

        /// <summary>SUCCESS! / Failed を真ん中に出す</summary>
        public static void DrawQteResult(Surface surface, bool success, (int x, int y) center)
        {
            if (success)
            {
                Utils.DrawText(surface, "SUCCESS!", Settings.FONT_LARGE, Settings.GLOW, Anchor.Center, center);
            }
            else
            {
                Utils.DrawText(surface, "Failed", Settings.FONT_LARGE, Settings.DAMAGE_RED, Anchor.Center, center);
            }
        }
    }
}
