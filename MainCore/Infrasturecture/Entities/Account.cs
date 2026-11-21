using System.ComponentModel.DataAnnotations.Schema;

using StronglyTypedIds;

#nullable disable

namespace MainCore.Infrasturecture.Entities
{
    public class Account
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Username { get; set; }
        public string Server { get; set; }

        /// <summary>Browser language, for example en-US. Empty: pick it from the current IP's country.</summary>
        public string Language { get; set; }

        /// <summary>IANA time zone for the browser and the night window. Empty: look it up from the current IP.</summary>
        public string TimeZone { get; set; }

        public AccountInfo Info { get; set; }

        public ICollection<AccountSetting> Settings { get; set; }
        public ICollection<Access> Accesses { get; set; }
        public ICollection<Village> Villages { get; set; }

        public ICollection<HeroItem> HeroItems { get; set; }
        public ICollection<Farm> FarmLists { get; set; }
    }

    [StronglyTypedId]
    public partial struct AccountId
    { }
}