import { DialogDescription } from "@radix-ui/react-dialog";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "./ui/dialog";

export const PasswordCrackerDialog = ({
  open,
  setOpen,
  responseMessage,
  isLoading,
}: {
  open: boolean;
  setOpen: (open: boolean) => void;
  responseMessage: string | null;
  isLoading: boolean;
}) => {
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader className="relative">
          <DialogTitle>Łamanie hasła</DialogTitle>{" "}
          <button
            className="absolute right-2 top-2 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-none"
            onClick={() => setOpen(false)}
          >
            <span className="sr-only">Close</span>
          </button>
        </DialogHeader>
        <div className="flex flex-col items-center space-y-4 py-4">
          {isLoading ? (
            <div className="flex flex-col items-center">
              <div className="loader border-t-4 border-blue-500 rounded-full w-8 h-8 animate-spin"></div>
              <span className="text-gray-500 mt-2">Ładowanie...</span>
            </div>
          ) : responseMessage ? (
            <div className="text-center font-bold text-primary">
              {responseMessage}
            </div>
          ) : (
            <div className="text-center text-gray-500">
              Oczekiwanie na odpowiedź...
            </div>
          )}
        </div>
        <DialogDescription />
      </DialogContent>
    </Dialog>
  );
};
