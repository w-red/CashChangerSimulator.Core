using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Xunit;

namespace CashChangerSimulator.Core.Tests.Models;

/// <summary>金種別数量レコードを検証するテストクラス。</summary>
public class CashDenominationCountTests
{
    /// <summary>コンストラクタでプロパティが正しく設定されることを検証します。</summary>
    [Fact]
    public void ConstructorSetsProperties()
    {
        // Arrange & Act
        var target = new CashDenominationCount(1000, 5);

        // Assert
        Assert.Equal(1000, target.Denomination);
        Assert.Equal(5, target.Count);
    }

    /// <summary>レコードとしての値比較（等価性）が正しく動作することを検証します。</summary>
    [Fact]
    public void EqualityWorks()
    {
        // Arrange
        var a = new CashDenominationCount(1000, 5);
        var b = new CashDenominationCount(1000, 5);
        var c = new CashDenominationCount(1000, 6);
        var d = new CashDenominationCount(500, 5);

        // Assert
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, d);
        Assert.True(a == b);
        Assert.False(a == c);
    }

    /// <summary>GetHashCode が等価なオブジェクトに対して同じ値を返すことを検証します。</summary>
    [Fact]
    public void GetHashCodeWorks()
    {
        // Arrange
        var a = new CashDenominationCount(1000, 5);
        var b = new CashDenominationCount(1000, 5);

        // Assert
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>ToString が型名とプロパティ値を含む文字列を返すことを検証します。</summary>
    [Fact]
    public void ToStringWorks()
    {
        // Arrange
        var target = new CashDenominationCount(1000, 5);

        // Act
        var result = target.ToString();

        // Assert
        Assert.Contains("1000", result);
        Assert.Contains("5", result);
    }
}
