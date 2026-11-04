public sealed class XUiC_RebirthBankBackpackWindow : XUiC_BackpackWindow
{
    public override void OnOpen()
    {
        if(!string.IsNullOrEmpty(defaultSelectedElement)&&GetChildById(defaultSelectedElement)==null)
            defaultSelectedElement=string.Empty;
        base.OnOpen();
    }
}
