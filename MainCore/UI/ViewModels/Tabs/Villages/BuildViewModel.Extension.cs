namespace MainCore.UI.ViewModels.Tabs.Villages
{
    public static class BuildViewModelExtension
    {
        /// <summary>
        /// Moves a job within its village's build queue and saves the new order. Positions are
        /// rewritten as 0..n-1 in one SaveChanges, so gaps or duplicates left by older versions
        /// heal on the first move. Returns the job's new index, or -1 if it no longer exists.
        /// </summary>
        public static int MoveJob(this AppDbContext context, JobId jobId, MoveEnums move)
        {
            var villageId = context.Jobs
                .Where(x => x.Id == jobId.Value)
                .Select(x => (int?)x.VillageId)
                .FirstOrDefault();

            if (villageId is null) return -1;

            var jobs = context.Jobs
                .Where(x => x.VillageId == villageId.Value)
                .OrderBy(x => x.Position)
                .ThenBy(x => x.Id)
                .ToList();

            var index = jobs.FindIndex(x => x.Id == jobId.Value);
            var job = jobs[index];

            var newIndex = move switch
            {
                MoveEnums.Top => 0,
                MoveEnums.Up => Math.Max(index - 1, 0),
                MoveEnums.Down => Math.Min(index + 1, jobs.Count - 1),
                MoveEnums.Bottom => jobs.Count - 1,
                _ => index,
            };

            jobs.RemoveAt(index);
            jobs.Insert(newIndex, job);

            for (var i = 0; i < jobs.Count; i++)
            {
                if (jobs[i].Position != i) jobs[i].Position = i;
            }

            context.SaveChanges();
            return newIndex;
        }

        public static void Upgrade(this AppDbContext context, VillageId villageId, int location, bool isMaxLevel)
        {
            var buildings = context.GetLayoutBuildings(villageId);
            var building = buildings.Find(x => x.Location == location);

            if (building is null) return;
            if (building.Type == BuildingEnums.Site) return;

            var level = 0;

            if (isMaxLevel)
            {
                level = building.Type.GetMaxLevel();
            }
            else
            {
                level = building.Level + 1;
            }

            var plan = new NormalBuildPlan()
            {
                Location = location,
                Type = building.Type,
                Level = level,
            };

            context.AddJob(villageId, plan);
        }

        public static Result CheckRequirements(this BuildingEnums buildingType, List<BuildingItem> buildings)
        {
            var prerequisiteBuildings = buildingType.GetPrerequisites();
            if (prerequisiteBuildings.Count == 0) return Result.Ok();
            IList<Error> errors = [];
            foreach (var prerequisiteBuilding in prerequisiteBuildings)
            {
                var valid = buildings
                    .Where(x => x.Type == prerequisiteBuilding.Type)
                    .Any(x => x.Level >= prerequisiteBuilding.Level);

                if (!valid) errors.Add(new Error($"Required {prerequisiteBuilding.Type} level {prerequisiteBuilding.Level}"));
            }
            return Result.FailIfNotEmpty(errors);
        }

        public static void FixLocation(this NormalBuildPlan plan, List<BuildingItem> buildings)
        {
            if (plan.Type.IsWall())
            {
                plan.Location = 40;
                return;
            }
            if (plan.Type.IsMultipleBuilding())
            {
                var sameTypeBuildings = buildings.Where(x => x.Type == plan.Type);
                if (!sameTypeBuildings.Any()) return;
                if (sameTypeBuildings.Any(x => x.Location == plan.Location)) return;
                var largestLevelBuilding = sameTypeBuildings.MaxBy(x => x.Level)!;
                if (largestLevelBuilding.Level == plan.Type.GetMaxLevel()) return;
                plan.Location = largestLevelBuilding.Location;
                return;
            }

            if (plan.Type.IsResourceField())
            {
                var field = buildings.First(x => x.Location == plan.Location);
                if (plan.Type == field.Type) return;
                plan.Type = field.Type;
                return;
            }

            var building = buildings.Find(x => x.Type == plan.Type);
            if (building is null) return;
            if (plan.Location == building.Location) return;
            plan.Location = building.Location;
        }
    }
}