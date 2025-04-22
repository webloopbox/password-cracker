import random
import string

def generate_common_passwords(num_passwords, output_file="common_passwords.txt"):
    """Generate num_passwords random passwords and save them to a plain text file."""
    common_patterns = [
        lambda: ''.join(random.choices(string.ascii_lowercase, k=random.randint(4, 8))), 
        lambda: ''.join(random.choices(string.ascii_letters, k=random.randint(6, 10))), 
        lambda: ''.join(random.choices(string.digits, k=random.randint(4, 8))),         
        lambda: ''.join(random.choices(string.ascii_letters + string.digits, k=random.randint(6, 12))),  
        lambda: ''.join(random.choices(string.ascii_lowercase + string.digits, k=random.randint(6, 10))),  
        lambda: ''.join(random.choices(string.ascii_letters + "!@#$%^&*", k=random.randint(6, 12))) 
    ]

    with open(output_file, "w") as file:
        for _ in range(num_passwords):
            password = random.choice(common_patterns)()
            file.write(password + "\n")

if __name__ == "__main__":
    NUM_PASSWORDS = 5_000_000 
    generate_common_passwords(NUM_PASSWORDS)