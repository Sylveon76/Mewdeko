using Mewdeko.Core.Services.Database.Models;
using System.Threading.Tasks;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IWarningsRepository2 : IRepository<Warning2>
    {
        Warning2[] ForId(ulong guildId, ulong userId);
        Task ForgiveAll(ulong guildId, ulong userId, string moderator);
        bool Forgive(ulong guildId, ulong userId, string moderator, int index);
        Warning2[] GetForGuild(ulong id);
    }
}
