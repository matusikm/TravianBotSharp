namespace MainCore.Features.Navigate
{
    public static class Parser
    {
        public static ILocator GetBuildPanel(this IPage page)
        {
            return page.Locator("#content.build");
        }

        public static ILocator GetTabs(this IPage page)
        {
            var tabs = page.Locator(".contentNavi.subNavi a.tabItem");
            return tabs;
        }

        public const string TabActiveExpression = "node => node.classList.contains('active')";

        public static async Task<bool> IsTabActive(this ILocator tab)
        {
            return await tab.EvaluateAsync<bool>(TabActiveExpression);
        }

        public static ILocator GetDorfButton(this IPage page, int dorf)
        {
            return dorf switch
            {
                1 => page.GetResourceButton(),
                _ => page.GetBuildingButton(),
            };
        }

        private static ILocator GetResourceButton(this IPage page) => page.GetButton(1);

        private static ILocator GetBuildingButton(this IPage page) => page.GetButton(2);

        public static ILocator GetVillageNode(this IPage page, VillageId villageId)
        {
            var node = page.Locator($"#sidebarBoxVillageList div.listEntry.village[data-did='{villageId}']");
            return node;
        }

        public static async Task<bool> IsActive(this ILocator locator)
        {
            return await locator.EvaluateAsync<bool>("node => node.classList.contains('active')");
        }

        public static ILocator GetFieldButton(this IPage page, int location)
        {
            var node = page.Locator($".village1 a.buildingSlot{location}");
            return node;
        }

        public static ILocator GetBuildingButton(this IPage page, int location)
        {
            // A slot can hold several clickable shapes: the building itself and, once hovered, a
            // g.highlightShape drawn on top of it with the same onclick. SVG paints later elements on
            // top, so take the last clickable path; the locator re-resolves at click time, after the
            // hover has added the highlight, so the click lands on the shape that receives it.
            var node = page.Locator($".village2 div.buildingSlot.a{location} svg path[onclick]");
            return node.Last;
        }

        public static async Task<int> GetBuildingLocation(this IPage page, BuildingEnums buildingType)
        {
            var buildingLocator = page.Locator($".village2 div.buildingSlot.g{(int)buildingType}");
            if (await buildingLocator.CountAsync() == 0)
            {
                return -1;
            }
            var location = await buildingLocator.GetAttributeAsync("data-aid");
            return int.Parse(location ?? "-1");
        }
    }
}