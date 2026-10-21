// Exact method body decompiled from the installed native PowerBatteryBank.
// Only its root-dirty callback is a disclosed fixture double.
using UnityEngine;
public sealed partial class PowerBatteryBank
{
	public void AddPowerToBatteries(int power)
	{
		int num = power;
		int b = power / (int)InputPerTick * ChargePerInput;
		for (int num2 = Stacks.Length - 1; num2 >= 0; num2--)
		{
			if (!Stacks[num2].IsEmpty())
			{
				int num3 = (int)Stacks[num2].itemValue.UseTimes;
				if (num3 > 0)
				{
					ushort num4 = (ushort)Mathf.Min(num3, b);
					num -= num4 * InputPerTick;
					Stacks[num2].itemValue.UseTimes -= (int)num4;
				}
				if (num == 0)
				{
					break;
				}
			}
		}
		int num5 = power - num;
		if (LastInputAmount != (ushort)num5)
		{
			SendHasLocalChangesToRoot();
			LastInputAmount = (ushort)num5;
		}
	}

}
