import type React from "react";

import { useState, useRef } from "react";
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
import axios, { AxiosResponse } from "axios";
import { BASE_URL } from "@/api";
import { CrackingResponse } from "@/interfaces/crackingResponse";

const formSchema = z.object({
  username: z
    .string()
    .min(2, { message: "Nazwa użytkownika musi mieć co najmniej 2 znaki." }),
  method: z.string(),
  passwordLength: z.coerce
    .number()
    .gte(2, "Hasło musi mieć co najmniej 2137 znaków.")
    .optional(),
  dictionaryFile: z
    .instanceof(File)
    .refine((file) => file.name.endsWith(".txt"), {
      message: "Plik musi być w formacie .txt.",
    })
    .optional(),
});

type FormData = z.infer<typeof formSchema>;

export default function PasswordCrackerForm() {
  const { control, handleSubmit, watch } = useForm<FormData>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      username: "user1",
      passwordLength: 2,
    },
  });
  const [dialogOpen, setDialogOpen] = useState<boolean>(false);
  const [selectedFileName, setSelectedFileName] = useState<string | null>(null);
  const [response, setResponse] = useState<CrackingResponse | undefined>(
    undefined
  );
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const methodSelected = watch("method");

  const onSubmit: SubmitHandler<FormData> = async (data) => {
    setDialogOpen(true);
    setIsLoading(true);

    try {
      let response: AxiosResponse<CrackingResponse> | undefined;
      if (data.method === "brute-force") {
        response = await axios.post(`${BASE_URL}/cracking/brute-force`, {
          userLogin: data.username,
          passwordLength: data.passwordLength,
        });
      } else if (data.method === "słownikowa") {
        const formData = new FormData();
        formData.append("username", data.username);
        if (data.dictionaryFile) {
          formData.append("file", data.dictionaryFile);
          await axios.post(`${BASE_URL}/synchronizing/dictionary`, formData, {
            headers: {
              "Content-Type": "multipart/form-data",
            },
          });
        }
        response = await axios.post(
          `${BASE_URL}/cracking/dictionary`,
          formData,
          {
            headers: {
              "Content-Type": "multipart/form-data",
            },
          }
        );
      }

      setResponse(response?.data);

      toast("Żądanie zostało wysłane pomyślnie!", {
        style: { background: "green", color: "white" },
      });
    } catch (error) {
      if (axios.isAxiosError(error)) {
        setResponse(error.response?.data);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];

    if (file) {
      if (!file.name.endsWith(".txt")) {
        toast("Plik musi być w formacie .txt.", {
          style: { background: "red", color: "white" },
        });
        return;
      }
      setSelectedFileName(file.name);
    }
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    const file = e.dataTransfer.files?.[0];
    if (file) {
      if (!file.name.endsWith(".txt")) {
        toast("Plik musi być w formacie .txt.", {
          style: { background: "red", color: "white" },
        });
        return;
      }
      if (fileInputRef.current) {
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(file);
        fileInputRef.current.files = dataTransfer.files;
      }
      setSelectedFileName(file.name);
    }
  };

  return (
    <>
      <form
        onSubmit={handleSubmit(onSubmit, (errors) => {
          Object.values(errors).forEach((error) => {
            if (error.message) {
              if (typeof error.message === "string") {
                toast(error.message, {
                  style: { background: "red", color: "white" },
                });
              }
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
                      min={2}
                      className="w-full sm:w-48 bg-gray-100"
                    />
                  )}
                />
              </div>
            </div>
          )}

          {methodSelected === "słownikowa" && (
            <div className="space-y-2">
              <div className="flex flex-col sm:flex-row justify-between sm:items-center">
                <Label
                  htmlFor="dictionaryFile"
                  className="text-blue-700 font-medium mb-2 sm:mb-0 sm:mr-4"
                >
                  Plik słownikowy
                </Label>
                <Controller
                  name="dictionaryFile"
                  control={control}
                  render={({ field: { onChange } }) => (
                    <div
                      className="w-full sm:w-48 border border-dashed rounded-md p-4 bg-gray-100 text-center cursor-pointer"
                      onDragOver={handleDragOver}
                      onDrop={handleDrop}
                      onClick={() => fileInputRef.current?.click()}
                    >
                      <input
                        ref={fileInputRef}
                        id="dictionaryFile"
                        type="file"
                        className="hidden"
                        onChange={(e) => {
                          handleFileChange(e);
                          onChange(e.target.files?.[0]);
                        }}
                      />
                      <div className="flex flex-col items-center justify-center">
                        <svg
                          className="w-8 h-8 text-gray-400 mb-2"
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                          xmlns="http://www.w3.org/2000/svg"
                        >
                          <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeWidth={2}
                            d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"
                          />
                        </svg>
                        <span className="text-sm text-gray-500">
                          {selectedFileName || "Przeciągnij i upuść"}
                        </span>
                      </div>
                    </div>
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

      <PasswordCrackerDialog
        open={dialogOpen}
        setOpen={setDialogOpen}
        response={response}
        isLoading={isLoading}
      />
    </>
  );
}
