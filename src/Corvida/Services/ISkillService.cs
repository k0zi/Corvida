using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface ISkillService
{
    Task<List<Skill>> GetSkillsAsync();
    Task<Skill> CreateSkillAsync(string name);
    Task SaveSkillAsync(Skill skill);
    Task DeleteSkillAsync(string id);
}
