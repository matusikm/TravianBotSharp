using ArchUnitNET.xUnitV3;

using Immediate.Handlers.Shared;

using MainCore.Shared.Constraints;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace MainCore.Test.ArchitectureTests
{
    public class NamingConventionTests : BaseTest
    {
        [Fact]
        public void Command_ShouldHave_NameEndingWith_Command()
        {
            Classes().That()
                .ImplementInterface(typeof(ICommand))
                .Should().HaveNameEndingWith("Command")
                .Check(Architecture);
        }

        [Fact]
        public void Task_ShouldHave_NameEndingWith_Task()
        {
            Classes().That()
                .ImplementInterface(typeof(ITask))
                .Should().HaveNameEndingWith("Task")
                .Check(Architecture);
        }

        [Fact]
        public void Behavior_ShouldHave_NameEndingWith_Behavior()
        {
            // Behavior<,> is an abstract class, not an interface, so match on inheritance.
            // Behaviors are generic, so their metadata names end in a backtick arity (`2).
            Classes().That()
                .AreAssignableTo(typeof(Behavior<,>))
                .And().AreNotAbstract()
                .Should().HaveNameMatching(@"Behavior(`\d+)?$")
                .Check(Architecture);
        }
    }
}