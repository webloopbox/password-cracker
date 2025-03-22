import { ArrowDown } from "lucide-react";
import { Dialog, DialogContent, DialogHeader } from "./ui/dialog";

export const PasswordCrackerDialog = ({
  open,
  setOpen,
}: {
  open: boolean;
  setOpen: (open: boolean) => void;
}) => {
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader className="relative">
          <button
            className="absolute right-2 top-2 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-none"
            onClick={() => setOpen(false)}
          >
            <span className="sr-only">Close</span>
          </button>
        </DialogHeader>
        <div className="flex flex-col items-center space-y-4 py-4">
          <div className="flex">
            <div className="flex">
              <div>
                <span className="font-bold text-primary">
                  1a7fcdd5a9fd4335232688834ded9b0
                </span>
                <sub className="ml-1">MD5</sub>
              </div>
            </div>
          </div>
          <ArrowDown className="h-5 w-5" />
          <div className="font-bold">zamek123</div>

          <div className="mt-4 text-center">
            <div>Łamanie zajęło 34:50</div>
            <div>Liczba prób: 3410</div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
};
