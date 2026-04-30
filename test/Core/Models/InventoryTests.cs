using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Models;

/// <summary>在庫管理(Inventory)の加算、設定、集計、シリアライズ機能を検証するためのテストクラス。</summary>
public class InventoryTests : CoreTestBase
{
    private readonly Mock<ILogger<Inventory>> mockLogger = new();

    public InventoryTests()
    {
    }

    /// <inheritdoc/>
    protected override Inventory CreateInventory()
    {
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return Inventory.Create(mockLogger.Object);
    }

    /// <summary>指定された金種の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddShouldIncreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(key, 5);
        Inventory.GetCount(key).ShouldBe(5);
    }

    /// <summary>すべてのバケット操作が正しい金種データと共に Changed イベントを発行することを検証する。</summary>
    [Fact]
    public void AllAddMethodsShouldNotifyCorrectKey()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var notifiedKeys = new List<DenominationKey>();
        Inventory.Changed.Subscribe(notifiedKeys.Add);

        Inventory.Add(key, 1);
        Inventory.AddCollection(key, 2);
        Inventory.AddReject(key, 3);
        Inventory.AddEscrow(key, 4);
        Inventory.ClearEscrow(); // ClearEscrow should notify of cleared keys

        notifiedKeys.Count.ShouldBeGreaterThanOrEqualTo(5);
        notifiedKeys.All(k => k == key).ShouldBeTrue();
    }

    /// <summary>枚数変化がない加算(0)の場合、Changed イベントが全く発行されないこと、および内部状態が不変であることを検証する。</summary>
    [Fact]
    public void AddZeroShouldNotNotifyAndNotChangeState()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var notificationCount = 0;
        using var sub = Inventory.Changed.Subscribe(_ => notificationCount++);

        Inventory.Add(key, 0);
        Inventory.AddCollection(key, 0);
        Inventory.AddReject(key, 0);
        Inventory.AddEscrow(key, 0);

        notificationCount.ShouldBe(0);
        Inventory.CalculateTotal().ShouldBe(0);
        InventoryPersistenceMapper.ToDictionary(Inventory).ShouldBeEmpty();
    }

    /// <summary>負の値を加算した際に数量が減少することを検証する。</summary>
    [Fact]
    public void AddNegativeShouldDecreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 10);
        Inventory.Add(key, -3);
        Inventory.GetCount(key).ShouldBe(7);
    }

    /// <summary>加算の結果が負になる場合に数量が0になることを検証する。</summary>
    [Fact]
    public void AddResultingInNegativeShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 5);
        Inventory.Add(key, -10);
        Inventory.GetCount(key).ShouldBe(0);
    }

    /// <summary>指定された金種の数量を直接設定できることを検証する。</summary>
    [Fact]
    public void SetCountShouldOverwriteCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 10);
        Inventory.SetCount(key, 5);
        Inventory.GetCount(key).ShouldBe(5);
    }

    /// <summary>負の数量を設定しようとした際に、操作が無視されることを検証する。</summary>
    [Fact]
    public void SetCountNegativeShouldBeIgnored()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 10);
        Inventory.SetCount(key, -5);
        Inventory.GetCount(key).ShouldBe(10);
    }

    /// <summary>回収庫の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddCollectionShouldIncreaseCollectionCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddCollection(key, 5);
        Inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        Inventory.HasDiscrepancy.ShouldBeTrue();
    }

    /// <summary>回収庫の加算結果が負になる場合に数量が0になることを検証する。</summary>
    [Fact]
    public void AddCollectionResultingInNegativeShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddCollection(key, 5);
        Inventory.AddCollection(key, -10);
        Inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    /// <summary>リジェクト庫の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddRejectShouldIncreaseRejectCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddReject(key, 5);
        Inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        Inventory.HasDiscrepancy.ShouldBeTrue();
    }

    /// <summary>リジェクト庫の加算結果が負になる場合に数量が0になることを検証する。</summary>
    [Fact]
    public void AddRejectResultingInNegativeShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddReject(key, 5);
        Inventory.AddReject(key, -10);
        Inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    /// <summary>通常庫、回収庫、リジェクト庫の合計金額を正しく計算できることを検証する。</summary>
    [Fact]
    public void CalculateTotalShouldIncludeAllSources()
    {
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var coin500 = new DenominationKey(500, CurrencyCashType.Coin);

        Inventory.Add(bill1000, 1);       // 1000
        Inventory.AddCollection(bill1000, 1); // 1000
        Inventory.AddReject(coin500, 1);     // 500

        Inventory.CalculateTotal().ShouldBe(2500);
    }

    /// <summary>数量0の加算操作が状態を変更しないことを検証する。</summary>
    [Fact]
    public void AddZeroShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(key, 0);
        Inventory.GetCount(key).ShouldBe(0);
    }

    /// <summary>回収庫への数量0の加算操作が状態を変更しないことを検証する。</summary>
    [Fact]
    public void AddCollectionZeroShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddCollection(key, 0);
        Inventory.CollectionCounts.ShouldBeEmpty();
    }

    /// <summary>リジェクト庫への数量0の加算操作が状態を変更しないことを検証する。</summary>
    [Fact]
    public void AddRejectZeroShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddReject(key, 0);
        Inventory.RejectCounts.ShouldBeEmpty();
    }

    /// <summary>通貨コードが空の場合にデフォルトの通貨コードが設定されることを検証する。</summary>
    [Fact]
    public void NormalizeKeyShouldSetDefaultCurrencyIfEmpty()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, string.Empty);
        Inventory.Add(key, 1);

        // デフォルト通貨コードが特定の値（JPY）であることを厳密にチェック
        Inventory.GetCount(key).ShouldBe(1);
        var counts = Inventory.AllCounts.ToList();
        counts.Count.ShouldBe(1);
        counts[0].Key.CurrencyCode.ShouldBe(DenominationKey.DefaultCurrencyCode);
        counts[0].Key.CurrencyCode.ShouldBe("JPY");
    }

    /// <summary>通貨コードを指定した合計金額の計算が、該当する通貨のみを集計することを検証する。複数金種を混ぜて Sum が Max に置換されるのを防ぐ。</summary>
    [Fact]
    public void CalculateTotalWithCurrencyCodeShouldOnlyIncludeMatches()
    {
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var jpy5000 = new DenominationKey(5000, CurrencyCashType.Bill, "JPY");
        var usd1 = new DenominationKey(1, CurrencyCashType.Bill, "USD");

        Inventory.Add(jpy1000, 2);
        Inventory.Add(jpy5000, 1);
        Inventory.Add(usd1, 10);

        // JPY のみの合計 (2*1000 + 1*5000 = 7000)
        // ここが Sum ではなく Max にされると 5000 になるため検知できる
        Inventory.CalculateTotal("JPY").ShouldBe(7000);

        // USD のみの合計
        Inventory.CalculateTotal("USD").ShouldBe(10);

        // 全体の合計 (7000 + 10 = 7010)
        Inventory.CalculateTotal().ShouldBe(7010);
    }

    /// <summary>ディクショナリ形式へのシリアライズとデシリアライズが正しく動作することを検証する。</summary>
    [Fact]
    public void DictionarySerializationShouldWork()
    {
        var pay = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        Inventory.Add(pay, 1);
        Inventory.AddCollection(pay, 2);
        Inventory.AddReject(pay, 3);

        var dict = InventoryPersistenceMapper.ToDictionary(Inventory);
        dict.Count.ShouldBe(3);

        // 正規表現や Contains ではなく、完全一致で形式を固定することで Stryker による微細な置換を検知する
        dict.ContainsKey("JPY:B1000").ShouldBeTrue();
        dict["JPY:B1000"].ShouldBe(1);
        dict.ContainsKey("COL:JPY:B1000").ShouldBeTrue();
        dict["COL:JPY:B1000"].ShouldBe(2);
        dict.ContainsKey("REJ:JPY:B1000").ShouldBeTrue();
        dict["REJ:JPY:B1000"].ShouldBe(3);

        // 文字列キーが空("")などに変異していないことを追加でアサート (Target M)
        dict.Keys.ShouldContain("JPY:B1000");
        dict.Keys.ShouldNotContain("");

        var newInventory = Inventory.Create();
        InventoryPersistenceMapper.LoadFromDictionary(newInventory, dict);
        newInventory.GetCount(pay).ShouldBe(1);
        newInventory.CollectionCounts.First(kv => kv.Key == pay).Value.ShouldBe(2);
        newInventory.RejectCounts.First(kv => kv.Key == pay).Value.ShouldBe(3);

        // 各カセットが混ざらずに独立してロードされていることを厳密に検証 (Target N)
        // (これにより LoadFromDictionary 内の If 判定の否定変異などを殺す)
        newInventory.AllCounts.Count(kv => kv.Value > 0).ShouldBe(1);
        newInventory.CollectionCounts.Count(kv => kv.Value > 0).ShouldBe(1);
        newInventory.RejectCounts.Count(kv => kv.Value > 0).ShouldBe(1);
    }

    /// <summary>LoadFromDictionary において不正な形式のキーが含まれている場合に適切に無視されることを検証する。</summary>
    [Fact]
    public void LoadFromDictionary_ShouldIgnoreInvalidFormat()
    {
        var dict = new Dictionary<string, int>
        {
            { "INVALID:KEY", 10 },    // 分割できない
            { "COL:INVALID:KEY", 5 }, // 3要素だがパース失敗
            { "COL:JPY:B1000", 2 }    // 正しい
        };

        var inventory = Inventory.Create();
        InventoryPersistenceMapper.LoadFromDictionary(inventory, dict);

        // 正しいものだけが取り込まれていること
        inventory.CollectionCounts.Count(kv => kv.Value > 0).ShouldBe(1);
        inventory.CollectionCounts.First(kv => kv.Value > 0).Value.ShouldBe(2);

        // さらに意地悪な形式を追加して、TryParseKey の失敗ルート (Boolean true 置換) を殺す
        var evilDict = new Dictionary<string, int>
        {
            { "COLJPYB1000", 100 },   // セパレータなし
            { "COL:JPYB1000", 200 },  // 3要素だが第3要素がパース不可
            { ":JPY:B1000", 300 },    // プレフィックス(COL/REJ)なし
            { "COL:JPY:B", 400 },     // 額面なし
        };

        inventory.Clear();
        var notifyCount = 0;
        using var sub = inventory.Changed.Subscribe(_ => notifyCount++);

        InventoryPersistenceMapper.LoadFromDictionary(inventory, evilDict);

        // 1つも処理されず、イベントも発生しないこと
        inventory.CalculateTotal().ShouldBe(0);
        notifyCount.ShouldBe(0);
    }

    /// <summary>LoadFromDictionary が大文字小文字を区別せずにプレフィックス(COL, REJ)を解釈できることを検証する。</summary>
    [Fact]
    public void LoadFromDictionaryShouldBeCaseInsensitive()
    {
        var pay = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var dict = new Dictionary<string, int>
        {
            { "col:JPY:B1000", 10 },
            { "reJ:JPY:B1000", 20 }
        };

        InventoryPersistenceMapper.LoadFromDictionary(Inventory, dict);
        Inventory.CollectionCounts.First(kv => kv.Key == pay).Value.ShouldBe(10);
        Inventory.RejectCounts.First(kv => kv.Key == pay).Value.ShouldBe(20);
    }

    /// <summary>不正なキーを含むディクショナリからの読み込みが、エラーなく無視されることを検証する。</summary>
    [Fact]
    public void LoadFromDictionaryInvalidKeyShouldIgnore()
    {
        var dict = new Dictionary<string, int> { { "INVALID", 10 } };
        InventoryPersistenceMapper.LoadFromDictionary(Inventory, dict); // Should not throw
        Inventory.CalculateTotal().ShouldBe(0);
    }

    /// <summary>不一致状態(Discrepancy)の設定と取得が正しく行われることを検証する。</summary>
    [Fact]
    public void SetDiscrepancyShouldWork()
    {
        Inventory.HasDiscrepancy.ShouldBeFalse();
        Inventory.HasDiscrepancy = true;
        Inventory.HasDiscrepancy.ShouldBeTrue();
    }

    /// <summary>不一致状態(HasDiscrepancy)が、通常庫・回収庫・リジェクト庫のいずれか一つでも存在する場合にTrueになることを網羅的に検証する。</summary>
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, true, true)]
    public void HasDiscrepancyShouldReflectMultipleSources(bool forced, bool hasCollection, bool hasReject, bool expected)
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        Inventory.HasDiscrepancy = forced;
        if (hasCollection) Inventory.AddCollection(key, 1);
        if (hasReject) Inventory.AddReject(key, 1);

        Inventory.HasDiscrepancy.ShouldBe(expected);
    }

    /// <summary>在庫が一度加算された後に減算されて 0 になり、辞書にキーだけが残っている状態でも不一致フラグが False になることを検証する。 (Target G)</summary>
    [Fact]
    public void HasDiscrepancy_ShouldBeFalse_WhenKeysExistWithZeroValue()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // 通常庫
        Inventory.Add(key, 1);
        Inventory.HasDiscrepancy.ShouldBeFalse(); // 通常庫は不一致に含まれない
        Inventory.Add(key, -1);

        // 回収庫
        Inventory.AddCollection(key, 1);
        Inventory.HasDiscrepancy.ShouldBeTrue();
        Inventory.AddCollection(key, -1);
        Inventory.HasDiscrepancy.ShouldBeFalse(); // 0に戻ったらFalseであるべき (Any v > 0)

        // リジェクト庫
        Inventory.AddReject(key, 1);
        Inventory.HasDiscrepancy.ShouldBeTrue();
        Inventory.AddReject(key, -1);
        Inventory.HasDiscrepancy.ShouldBeFalse();
    }

    /// <summary>エスクロー(投入中)の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddEscrowShouldIncreaseEscrowCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddEscrow(key, 5);
        Inventory.EscrowCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
    }

    /// <summary>エスクローの数量をリセットできることを検証する。</summary>
    [Fact]
    public void ClearEscrowShouldResetEscrowCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddEscrow(key, 5);

        var notifyCount = 0;
        using var sub = Inventory.Changed.Subscribe(_ => notifyCount++);

        Inventory.ClearEscrow();
        Inventory.EscrowCounts.ShouldBeEmpty();
        notifyCount.ShouldBe(1);

        // すでに空の状態でもう一度 Clear してもイベントは飛ばないこと
        Inventory.ClearEscrow();
        notifyCount.ShouldBe(1); // カウントが増えていないこと
    }

    /// <summary>エスクローの加算結果が負になる場合に数量が0になることを検証する。</summary>
    [Fact]
    public void AddEscrowResultingInNegativeShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddEscrow(key, 5);
        Inventory.AddEscrow(key, -10);
        Inventory.EscrowCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    /// <summary>合計金額の計算にエスクローの金額が含まれることを検証する。</summary>
    [Fact]
    public void CalculateTotalShouldIncludeEscrow()
    {
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(bill1000, 1);       // 1000
        Inventory.AddEscrow(bill1000, 2); // 2000
        Inventory.CalculateTotal().ShouldBe(3000);
    }

    /// <summary>4つのすべてのカセット(通常・回収・リジェクト・エスクロー)に複数金種がある状態での合計計算を検証する。 (Target F)</summary>
    [Fact]
    public void CalculateTotal_ShouldBeCorrect_AcrossAllCassettesWithMultipleDenominations()
    {
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var jpy5000 = new DenominationKey(5000, CurrencyCashType.Bill, "JPY");

        // 4つのカセット各々に「1000円x2, 5000円x1 = 7000円」をセット
        // 合計 7000 * 4 = 28000
        Action<DenominationKey, int>[] addMethods = [
            Inventory.Add,
            Inventory.AddCollection,
            Inventory.AddReject,
            Inventory.AddEscrow
        ];

        foreach (var add in addMethods)
        {
            add(jpy1000, 2);
            add(jpy5000, 1);
        }

        // どこか一箇所でも Sum -> Max (5000) になると 28000 に届かないため検知可能
        Inventory.CalculateTotal("JPY").ShouldBe(28000);
        Inventory.CalculateTotal().ShouldBe(28000);
    }


    /// <summary>すべてのバケットに対して最小値境界(-1)の動作を検証する。</summary>
    [Fact]
    public void AllBucketsShouldNotGoBelowZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // 初期状態 0 から -1 する
        Inventory.Add(key, -1);
        Inventory.AddCollection(key, -1);
        Inventory.AddReject(key, -1);
        Inventory.AddEscrow(key, -1);

        Inventory.GetCount(key).ShouldBe(0);

        // エントリーが存在しても、値が 0 であることを検証する
        Inventory.CollectionCounts.All(kv => kv.Value == 0).ShouldBeTrue();
        Inventory.RejectCounts.All(kv => kv.Value == 0).ShouldBeTrue();
        Inventory.EscrowCounts.All(kv => kv.Value == 0).ShouldBeTrue();
    }

    /// <summary>不一致状態(HasDiscrepancy)を外部から直接変更できることを検証する。</summary>
    [Fact]
    public void HasDiscrepancySetterShouldWork()
    {
        Inventory.HasDiscrepancy = true;
        Inventory.HasDiscrepancy.ShouldBeTrue();
        Inventory.HasDiscrepancy = false;
        Inventory.HasDiscrepancy.ShouldBeFalse();
    }

    /// <summary>GetTotalCount がすべてのバケットを合計することを検証する。</summary>
    [Fact]
    public void GetTotalCountShouldSumAllBuckets()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(key, 10);
        Inventory.AddCollection(key, 5);
        Inventory.AddReject(key, 3);
        Inventory.AddEscrow(key, 2);

        Inventory.GetTotalCount(key).ShouldBe(20);
    }

    /// <summary>Clear メソッドがすべてのバケットを初期化することを検証する。</summary>
    [Fact]
    public void ClearShouldResetAllBuckets()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(key, 10);
        Inventory.AddCollection(key, 5);
        Inventory.AddReject(key, 3);
        Inventory.AddEscrow(key, 2);

        Inventory.Clear();

        Inventory.GetCount(key).ShouldBe(0);
        Inventory.CollectionCounts.ShouldBeEmpty();
        Inventory.RejectCounts.ShouldBeEmpty();
        Inventory.EscrowCounts.ShouldBeEmpty();
    }

    /// <summary>Dispose 時に Subject が実際に破棄されることを検証し、Dispose ライフサイクルの実在を証明する。 (Target J)</summary>
    [Fact]
    public void DisposeShouldDisposeSubject()
    {
        var inv = Inventory.Create(mockLogger.Object);
        var subject = (Subject<DenominationKey>)inv.Changed;

        // 破棄前は Disposed ではない
        subject.IsDisposed.ShouldBeFalse();

        inv.Dispose();

        // 破棄後は実際に Disposed になっていること (これにより Dispose(bool) 内の変異を殺す)
        subject.IsDisposed.ShouldBeTrue();

        // 破棄後に操作を行った場合、ObjectDisposedException がスローされること (Target O)
        Should.Throw<ObjectDisposedException>(() => inv.Add(new DenominationKey(1000, CurrencyCashType.Bill), 1));
        Should.Throw<ObjectDisposedException>(() => inv.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 1));
        Should.Throw<ObjectDisposedException>(inv.Clear);
        Should.Throw<ObjectDisposedException>(() => inv.CalculateTotal());
        Should.Throw<ObjectDisposedException>(() => InventoryPersistenceMapper.ToDictionary(inv));

        // 2回目の呼出も安全であり、状態が変わらないこと
        inv.Dispose();
        subject.IsDisposed.ShouldBeTrue();
    }

    /// <summary>LoadFromDictionary において、プレフィックスは正しいがキー形式が不正な場合、無視されることを検証する。</summary>
    [Fact]
    public void LoadFromDictionaryShouldIgnoreInvalidKeysWithPrefixes()
    {
        var data = new Dictionary<string, int>
        {
            { "COL:invalid", 100 },
            { "REJ:too:many:parts:here", 200 },
            { "COL:", 300 },
            { "REJ:", 400 },
            { "", 500 },
            { "INVALID:PREFIX:JPY:B1000", 600 }
        };
        InventoryPersistenceMapper.LoadFromDictionary(Inventory, data);

        Inventory.CollectionCounts.ShouldBeEmpty();
        Inventory.RejectCounts.ShouldBeEmpty();
        Inventory.CalculateTotal().ShouldBe(0);
    }

    /// <summary>ToDictionary が正しいプレフィックス（通常在庫はプレフィックスなし）を使用していることを検証する。</summary>
    [Fact]
    public void ToDictionaryShouldUseCorrectPrefixes()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        Inventory.Add(key, 10);
        Inventory.AddCollection(key, 5);
        Inventory.AddReject(key, 3);

        var dict = InventoryPersistenceMapper.ToDictionary(Inventory);

        // プレフィックスなしが通常在庫
        dict.ShouldContainKey("JPY:B1000");
        dict["JPY:B1000"].ShouldBe(10);

        dict.ShouldContainKey("COL:JPY:B1000");
        dict["COL:JPY:B1000"].ShouldBe(5);

        dict.ShouldContainKey("REJ:JPY:B1000");
        dict["REJ:JPY:B1000"].ShouldBe(3);
    }

    /// <summary>dispose 済みの場合に GetTotalCount も例外を投げるべきか検討（現在はガードがないことを確認）。</summary>
    /// <remarks>上位ガードが消された際の影響を確認するため、追加のガード検証を行う。</remarks>
    [Fact]
    public void OperationsAfterDisposeShouldThrowObjectDisposedExceptionFull()
    {
        var inv = Inventory.Create();
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inv.Dispose();

        Should.Throw<ObjectDisposedException>(() => inv.Add(key, 1));
        Should.Throw<ObjectDisposedException>(() => inv.SetCount(key, 1));
        Should.Throw<ObjectDisposedException>(() => inv.AddCollection(key, 1));
        Should.Throw<ObjectDisposedException>(() => inv.AddReject(key, 1));
        Should.Throw<ObjectDisposedException>(() => inv.AddEscrow(key, 1));
        Should.Throw<ObjectDisposedException>(inv.ClearEscrow);
        Should.Throw<ObjectDisposedException>(inv.Clear);
        Should.Throw<ObjectDisposedException>(() => inv.CalculateTotal());
        Should.Throw<ObjectDisposedException>(() => InventoryPersistenceMapper.ToDictionary(inv));
        Should.Throw<ObjectDisposedException>(() => InventoryPersistenceMapper.LoadFromDictionary(inv, new Dictionary<string, int>()));
    }

    /// <summary>数量の更新(Add/SetCount)によって合計枚数が負数になる場合、警告ログを出力し、0にクランプされることを検証する。(Target L)</summary>
    [Fact]
    public void UpdateCountNegativeTotalShouldLogWarningAndClamp()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // 1. SetCount で負数を指定 -> 0にクランプ & 警告
        Inventory.SetCount(key, -5);
        Inventory.GetCount(key).ShouldBe(0);

        // 2. Add で負数を指定して合計を負数にする -> 0にクランプ & 警告
        Inventory.SetCount(key, 10);
        Inventory.Add(key, -15);
        Inventory.GetCount(key).ShouldBe(0);

        // 警告ログが少なくとも2回（SetCount時とAdd時）出力されていることを検証
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    /// <summary>LoadFromDictionary において、無効な形式のキー文字列が正しく無視されることを検証する。(Target P)</summary>
    [Theory]
    [InlineData("INVALID")]           // 形式不正
    [InlineData("JPY:X1000")]        // タイプ不明
    [InlineData("JPY:B")]            // 額面なし
    [InlineData("JPY:B:1000:EXTRA")] // セグメント多すぎ
    [InlineData(":B1000")]           // 通貨コード空
    [InlineData("COL:INVALID")]      // プレフィックスあり・形式不正
    public void LoadFromDictionaryWithInvalidFormatsShouldIgnore(string input)
    {
        var data = new Dictionary<string, int> { { input, 100 } };
        InventoryPersistenceMapper.LoadFromDictionary(Inventory, data);
        Inventory.CalculateTotal().ShouldBe(0);
    }

    /// <summary>CurrencyCode が null の金種キーが NormalizeKey によってデフォルト値(JPY)に変換されることを検証します。</summary>
    [Fact]
    public void AddShouldNormalizeNullCurrencyCode()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, null!);

        Inventory.Add(key, 1);

        var counts = Inventory.AllCounts.ToList();
        counts.Count.ShouldBe(1);
        counts[0].Key.CurrencyCode.ShouldBe(DenominationKey.DefaultCurrencyCode);
    }

    /// <summary>カセットの更新において、結果が負数になる場合に0にクランプされ、かつ適切な警告ログが出力されることを検証します。</summary>
    [Fact]
    public void UpdateBucketShouldClampToZeroAndLogWarning()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // 通常庫で 5 -> -10 (結果 -5)
        Inventory.SetCount(key, 5);
        Inventory.Add(key, -10);
        Inventory.GetCount(key).ShouldBe(0);

        // 回収庫で 2 -> -5 (結果 -3)
        Inventory.AddCollection(key, 2);
        Inventory.AddCollection(key, -5);
        Inventory.CollectionCounts.First(kv => kv.Key == key).Value.ShouldBe(0);

        // 警告ログが2回出力されていることの検証 (Target L のさらなる強化)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    /// <summary>複数のスレッドから同時に在庫を加算した際に、最終的な合計値が正しく、かつ不整合が起きないことを検証します。(lock 削除変異対策)</summary>
    [Fact]
    public async Task ConcurrentAddShouldBeThreadSafe()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        const int ThreadCount = 10;
        const int AddsPerThread = 1000;
        var ct = TestContext.Current.CancellationToken;

        var tasks = new Task[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < AddsPerThread; j++)
                {
                    Inventory.Add(key, 1);
                }
            }, ct);
        }

        await Task.WhenAll(tasks);

        // 合計が ThreadCount * AddsPerThread に一致すること
        // もし lock (@lock) が削除されていると、レースコンディションにより合計が少なくなります
        Inventory.GetCount(key).ShouldBe(ThreadCount * AddsPerThread);
    }

    /// <summary>金種キーや辞書を引数にとるすべての公開メソッドが、null を渡された際に ArgumentNullException を投げることを網羅的に検証します。(ThrowIfNull 削除変異対策)</summary>
    [Fact]
    public void AllMethodsShouldThrowArgumentNullExceptionForNullKey()
    {
        Should.Throw<ArgumentNullException>(() => Inventory.Add(null!, 1));
        Should.Throw<ArgumentNullException>(() => Inventory.SetCount(null!, 1));
        Should.Throw<ArgumentNullException>(() => Inventory.AddCollection(null!, 1));
        Should.Throw<ArgumentNullException>(() => Inventory.AddReject(null!, 1));
        Should.Throw<ArgumentNullException>(() => Inventory.AddEscrow(null!, 1));
        Should.Throw<ArgumentNullException>(() => Inventory.GetCount(null!));
        Should.Throw<ArgumentNullException>(() => Inventory.GetTotalCount(null!));
        Should.Throw<ArgumentNullException>(() => InventoryPersistenceMapper.LoadFromDictionary(Inventory, null!));
    }
}
