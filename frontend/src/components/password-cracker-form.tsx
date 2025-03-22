import { useState } from "react";
import { useForm, Controller, type SubmitHandler } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { PasswordCrackerDialog } from "./password-cracker-dialog";

const formSchema = z.object({
  username: z
    .string()
    .min(2, { message: "Nazwa użytkownika musi mieć co najmniej 2 znaki." }),
  method: z.string(),
  passwordLength: z.coerce
    .number()
    .gte(5, "Hasło musi mieć co najmniej 6 znaków.")
    .optional(),
  hosts: z.coerce.number().gte(2, "Liczba hostów musi być większa od 1."),
});

type FormData = z.infer<typeof formSchema>;

export default function PasswordCrackerForm() {
  const { control, handleSubmit, watch } = useForm<FormData>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      username: "user1",
      hosts: 10,
      passwordLength: 6,
    },
  });
  const [dialogOpen, setDialogOpen] = useState<boolean>(false);

  const methodSelected = watch("method");

  const onSubmit: SubmitHandler<FormData> = async (data) => {
    console.log(data);
    setDialogOpen(true);
  };

  return (
    <>
      <form
        onSubmit={handleSubmit(onSubmit, (errors) => {
          Object.values(errors).forEach((error) => {
            if (error.message) {
              toast(error.message, {
                style: { background: "red", color: "white" },
              });
            }
          });
        })}
        className="w-full max-w-xl mx-auto min-h-[80vh] flex flex-col justify-between"
      >
        <div className="space-y-6">
          <div className="text-center">
            <h1 className="text-xl md:text-4xl font-bold mb-24 ">
              Łamacz haseł - System rozproszony
            </h1>
          </div>

          <div className="space-y-2">
            <div className="flex flex-col sm:flex-row justify-between sm:items-center">
              <Label
                htmlFor="username"
                className="text-blue-700 font-medium mb-2 sm:mb-0 sm:mr-4"
              >
                Wprowadź login użytkownika
              </Label>
              <Controller
                name="username"
                control={control}
                render={({ field }) => (
                  <Input
                    {...field}
                    id="username"
                    className="w-full sm:w-48 bg-gray-100"
                  />
                )}
              />
            </div>
          </div>

          <div className="space-y-2">
            <div className="flex flex-col sm:flex-row justify-between sm:items-center">
              <Label
                htmlFor="method"
                className="text-blue-700 font-medium mb-2 sm:mb-0 sm:mr-4"
              >
                Wybierz metodę łamania
              </Label>
              <Controller
                name="method"
                control={control}
                render={({ field }) => (
                  <Select
                    {...field}
                    onValueChange={(value) => {
                      field.onChange(value);
                    }}
                  >
                    <SelectTrigger className="w-full sm:w-48 bg-gray-100">
                      <SelectValue placeholder="Wybierz metodę" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="brute-force">brute-force</SelectItem>
                      <SelectItem value="słownikowa">słownikowa</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          </div>

          {methodSelected === "brute-force" && (
            <div className="space-y-2">
              <div className="flex flex-col sm:flex-row justify-between sm:items-center">
                <Label
                  htmlFor="passwordLength"
                  className="text-blue-700 font-medium mb-2 sm:mb-0 sm:mr-4"
                >
                  Ilość znaków hasła
                </Label>
                <Controller
                  name="passwordLength"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      id="passwordLength"
                      type="number"
                      className="w-full sm:w-48 bg-gray-100"
                    />
                  )}
                />
              </div>
            </div>
          )}

          {methodSelected && (
            <div className="space-y-2">
              <div className="flex flex-col sm:flex-row justify-between sm:items-center">
                <Label
                  htmlFor="hosts"
                  className="text-blue-700 font-medium mb-2 sm:mb-0 sm:mr-4"
                >
                  Ilość hostów
                </Label>
                <Controller
                  name="hosts"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      id="hosts"
                      type="number"
                      className="w-full sm:w-48 bg-gray-100"
                    />
                  )}
                />
              </div>
            </div>
          )}
        </div>

        <div className="pt-4 flex justify-center">
          {methodSelected && (
            <Button
              type="submit"
              className="bg-primary rounded-full p-6 cursor-pointer"
            >
              Rozpocznij łamanie
            </Button>
          )}
        </div>
      </form>

      <PasswordCrackerDialog open={dialogOpen} setOpen={setDialogOpen} />
    </>
  );
}
