// プレイヤーと敵のパラメータを持つクラス (Pygame 版の characters.py)
//
//   Stats     … 基本パラメータ(攻撃力・HP・呪力)。HP は上限なし、ほかは 0〜100
//   Character … プレイヤーと敵に共通する部分(今のHP・今の呪力・通常こうげき など)
//   Player    … プレイヤー。QTEポイントを持つ
//   Enemy     … 敵。種族(鬼・怨霊・骸骨…)でパラメータが決まる。防御力・ダメージカット率K を持つ
//
// 行動の順番は いつも「敵のこうげき(防御QTE) → プレイヤー」(Scenes/BattleScene.cs)
//
// パラメータの数値は data/characters.csv にあり、倍率はかけずにそのまま使う
using System;
using System.Collections.Generic;

namespace QteRpg
{
    public sealed class Stats
    {
        public int Attack;  // 攻撃力
        public int Hp;      // HP(最大HP)
        public int Mp;      // 呪力(MP)パラメータ

        public Stats(int attack, int hp, int mp)
        {
            // HP 以外のパラメータは 0〜STAT_MAX(100) におさめる。HP は上限なし(0 より下にはしない)
            Attack = Py.Clamp(attack, 0, GameData.STAT_MAX);
            Mp = Py.Clamp(mp, 0, GameData.STAT_MAX);
            Hp = Math.Max(0, hp);
        }

        /// <summary>CSV から読んだ辞書 {"attack": .., "hp": .., ...} から作る</summary>
        public static Stats FromDict(IDictionary<string, int> data) => new Stats(data["attack"], data["hp"], data["mp"]);

        public int Get(string name)
        {
            switch (name)
            {
                case "attack": return Attack;
                case "hp": return Hp;
                case "mp": return Mp;
                default: throw new KeyNotFoundException(name);
            }
        }
    }

    public static class Combat
    {
        /// <summary>
        /// 小数を切り上げて整数にする
        /// 小数の計算の誤差(例: 25 × 2.2 = 55.00000000000001)で余計に切り上がらないよう、先に丸めておく
        /// </summary>
        public static int RoundUp(double value) => (int)Math.Ceiling(Math.Round(value, 6));

        /// <summary>
        /// プレイヤーの通常こうげきのダメージ: 攻撃力 × K ÷ (K + 敵の防御力) × rate を切り上げた整数
        /// K は data/battle_rules.csv の「防御力の効き(K)」。防御力が K と同じならダメージは半分
        /// </summary>
        public static int CalcDamage(int attack, int defense, double rate = 1.0)
        {
            double k = GameData.DEFENSE_K;
            return RoundUp(attack * k / (k + defense) * rate);
        }

        /// <summary>
        /// 防御QTEのあとに受けるダメージ: 攻撃力 − K × ガードできた割合 を切り上げた整数(0 より下にはしない)
        ///   cut        … その敵のダメージカット率K
        ///   guardRatio … ガードできた数 ÷ その敵のキーの数(0〜1)
        /// </summary>
        public static int GuardedDamage(int attack, double cut, double guardRatio) => Math.Max(0, RoundUp(attack - cut * guardRatio));
    }

    /// <summary>攻撃力を使うときは Stat("attack") のように、上がった分もふくめた値を使う</summary>
    public class Character
    {
        public string Name;
        public Stats Stats;
        public int MaxHp;   // HPの最大値(パラメータのまま)
        public int MaxMp;   // 呪力の最大値(パラメータのまま)
        public int Hp;      // 今のHP(最初は満タン)
        public int Mp;      // 今の呪力(最初は満タン)
        public Dictionary<string, int> Buffs = new Dictionary<string, int> { { "attack", 0 } };  // じゅふなどで上がっている分
        public int Defense; // 防御力(敵だけが持つ。プレイヤーは 0)

        public Character(string name, Stats stats)
        {
            Name = name;
            Stats = stats;
            MaxHp = stats.Hp;
            MaxMp = stats.Mp;
            Hp = MaxHp;
            Mp = MaxMp;
            Defense = 0;
        }

        /// <summary>上がった分もふくめた今のパラメータ("attack")。0〜STAT_MAX(100)</summary>
        public int Stat(string name) => Py.Clamp(Stats.Get(name) + Buffs.GetOr(name, 0), 0, GameData.STAT_MAX);

        /// <summary>パラメータを上げる(バトルのあいだだけ)</summary>
        public void RaiseStat(string name, int amount) => Buffs[name] += amount;

        /// <summary>上がった分をもとにもどす(バトルの始めに呼ぶ)</summary>
        public void ClearBuffs()
        {
            foreach (var name in new List<string>(Buffs.Keys))
            {
                Buffs[name] = 0;
            }
        }

        public bool IsAlive => Hp > 0;

        /// <summary>HPを減らす(0より下にはならない)</summary>
        public void TakeDamage(int amount) => Hp = Py.Clamp(Hp - amount, 0, MaxHp);

        /// <summary>HPを回復する(最大値より上にはならない)</summary>
        public void Heal(int amount) => Hp = Py.Clamp(Hp + amount, 0, MaxHp);

        /// <summary>呪力を使う。足りなければ何もしないで false を返す</summary>
        public bool UseMp(int amount)
        {
            if (Mp < amount)
            {
                return false;
            }
            Mp -= amount;
            return true;
        }

        /// <summary>
        /// 通常こうげき: 相手のHPを減らして、与えたダメージを返す
        ///   ダメージ = 攻撃力 × K ÷ (K + 相手の防御力) × rate  (最後に小数を切り上げ)
        ///   rate は 攻撃QTE の結果で変わる(成功で「QTE成功で与えるダメージ倍率」)
        /// </summary>
        public int NormalAttack(Character target, double rate = 1.0)
        {
            int damage = Combat.CalcDamage(Stat("attack"), target.Defense, rate);
            target.TakeDamage(damage);
            return damage;
        }
    }

    public sealed class Player : Character
    {
        public int QtePoints;  // QTEポイント

        public Player() : base(GameData.PLAYER_NAME, Stats.FromDict(GameData.PLAYER_STATS))
        {
            QtePoints = Py.Clamp(GameData.PLAYER_QTE_START, 0, GameData.QTE_MAX);
        }

        /// <summary>QTEに成功したときに呼ぶ。QTE_MAX(5) より増えない</summary>
        public void AddQtePoint(int amount = 1) => QtePoints = Py.Clamp(QtePoints + amount, 0, GameData.QTE_MAX);

        /// <summary>QTEポイントが5つ(QTE_MAX)たまっているか → ひっさつが使える</summary>
        public bool QteFull => QtePoints >= GameData.QTE_MAX;

        /// <summary>ひっさつを使ったときに呼ぶ。QTEポイントを0にもどす</summary>
        public void UseQtePoints() => QtePoints = 0;

        /// <summary>QTEポイントを amount 減らす(じゅふを使ったときなど)。0 より下にはならない</summary>
        public void LoseQtePoints(int amount = 1) => QtePoints = Py.Clamp(QtePoints - amount, 0, GameData.QTE_MAX);
    }

    public sealed class Enemy : Character
    {
        public string Species;  // 種族名("鬼" など。画面に出す名前)
        public string Folder;   // 画像フォルダの名前(assets/images/Enemy/<Folder>)
        public double Cut;      // ダメージカット率K(防御QTEを全部ガードすると、攻撃力からこの分へる)

        public Enemy(string species) : this(species, GameData.ENEMY_SPECIES[species]) { }

        Enemy(string species, EnemySpeciesData data) : base(species, new Stats(data.Attack, data.Hp, data.Mp))
        {
            Species = species;
            Folder = data.Folder;
            Cut = data.Cut;
            Defense = Py.Clamp(data.Defense, 0, GameData.STAT_MAX);  // 防御力(プレイヤーのこうげきを弱める)
        }

        /// <summary>防御QTEのあとにプレイヤーが受けるダメージ = 攻撃力 − K × (ガードできた数 ÷ この敵のキーの数)</summary>
        public int DamageAfterGuard(double guardRatio) => Combat.GuardedDamage(Stat("attack"), Cut, guardRatio);

        /// <summary>
        /// この敵の行動を決める
        /// 今は通常こうげき("attack")だけ。ひっさつ(呪力を使う)はあとでここに追加する
        /// </summary>
        public string ChooseAction() => "attack";
    }
}
