using System.Collections.Generic;
using System.Threading.Tasks;
using GuidesFusion360Server.Models;

namespace GuidesFusion360Server.Data.Repositories
{
    /// <summary>Repository to work with users db data.</summary>
    public interface IUsersRepository
    {
        /// <summary>Add user to db.</summary>
        /// <param name="user">User data.</param>
        /// <returns>Returns user id.</returns>
        Task<int> AddUser(User user);

        /// <summary>Gets user by id.</summary>
        /// <param name="userId">Id of the user.</param>
        /// <returns>Returns user data.</returns>
        Task<User> GetUser(int userId);

        /// <summary>Gets user by email.</summary>
        /// <param name="email">Email of the user.</param>
        /// <returns>Returns user data.</returns>
        Task<User> GetUser(string email);

        /// <summary>Gets all users data.</summary>
        /// <returns>Returns list of all users.</returns>
        Task<List<User>> GetUsers();

        /// <summary>Checks if user with given email exists.</summary>
        /// <param name="email">Email of the user.</param>
        /// <returns>Returns 'true' if exists.</returns>
        Task<bool> UserExists(string email);

        /// <summary>Checks if user with given id is editor or admin.</summary>
        /// <param name="userId">Id of the user.</param>
        /// <returns>Returns 'true' if user is editor or admin.</returns>
        Task<bool> UserIsEditor(int userId);

        /// <summary>Checks if user with given id is admin.</summary>
        /// <param name="userId">Id of the user.</param>
        /// <returns>Returns 'true' is user is admin.</returns>
        Task<bool> UserIsAdmin(int userId);

        /// <summary>Updates user data.</summary>
        /// <param name="user">User data.</param>
        Task UpdateUser(User user);

        /// <summary>Removes user.</summary>
        /// <param name="user">User data.</param>
        Task RemoveUser(User user);
    }
}