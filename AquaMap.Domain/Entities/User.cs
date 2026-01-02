using System;

namespace AquaMap.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string FullName { get; private set; }
        public string TaxId { get; private set; }
        public DateTime BirthDate { get; private set; }
        public string Address { get; private set; }
        public string PhoneNumber { get; private set; }
        public string Email { get; private set; }
        public string PasswordHash { get; private set; }
        public UserType Role { get; private set; }

        // Atualize o construtor para receber TODOS os campos obrigatórios
        public User(string fullName, string taxId, DateTime birthDate, string address, string phoneNumber, string email, string passwordHash, UserType role)
        {
            Id = Guid.NewGuid();
            FullName = fullName;
            TaxId = taxId;
            BirthDate = birthDate; // Adicionado
            Address = address;     // Adicionado
            PhoneNumber = phoneNumber; // Adicionado
            Email = email;
            PasswordHash = passwordHash;
            Role = role;
        }
    }

    public enum UserType
    {
        Citizen,
        Administrator
    }
}