import { DialogDescription } from "@radix-ui/react-dialog";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "./ui/dialog";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import type { CrackingResponse } from "@/interfaces/crackingResponse";
import { AnimatedList } from "@/components/magicui/animated-list";
import { HyperText } from "./magicui/hyper-text";
import { ShineBorder } from "./magicui/shine-border";

export const PasswordCrackerDialog = ({
  open,
  setOpen,
  response,
  isLoading,
}: {
  open: boolean;
  setOpen: (open: boolean) => void;
  response?: CrackingResponse;
  isLoading: boolean;
}) => {
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent className="w-[800px] min-h-[680px] flex flex-col justify-between">
        <DialogHeader className="relative">
          <DialogTitle className="text-2xl font-bold">
            Łamanie hasła
          </DialogTitle>
          <button
            className="absolute right-2 top-2 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-none"
            onClick={() => setOpen(false)}
          >
            <span className="sr-only">Zamknij</span>
          </button>
        </DialogHeader>
        <div className="flex flex-col items-center space-y-4 py-4 flex-grow">
          {isLoading ? (
            <div className="flex flex-col items-center">
              <div className="loader border-t-4 border-blue-500 rounded-full w-8 h-8 animate-spin"></div>
              <span className="text-gray-500 mt-2">Ładowanie...</span>
            </div>
          ) : response ? (
            <div className="">
              {/* Message */}
              <div className="col-span-full">
                <HyperText className="text-center text-primary font-bold text-lg">
                  {response.message}
                </HyperText>
              </div>

              <AnimatedList
                className="grid grid-cols-1 md:grid-cols-2 gap-4 w-full mt-4 items-start"
                delay={250}
              >
                {/* Execution Times */}
                <Card className="relative overflow-hidden">
                  <ShineBorder shineColor={["#A07CFE", "#FE8FB5", "#FFBE7B"]} />
                  <CardHeader>
                    <CardTitle>Czas wykonania</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <p>Całkowity czas: {response.totalExecutionTime} ms</p>
                    <p>Średni czas serwera: {response.averageServerTime} ms</p>
                    <p>Czas komunikacji: {response.communicationTime} ms</p>
                  </CardContent>
                </Card>

                {/* Server Times */}
                <Card className="relative overflow-hidden">
                  <ShineBorder shineColor={["#A07CFE", "#FE8FB5", "#FFBE7B"]} />
                  <CardHeader>
                    <CardTitle>Czasy serwerów</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <ul className="list-disc pl-5">
                      {response.serversTimes.map((serverTime, index) => (
                        <li key={index}>
                          Serwer {serverTime.server}: {serverTime.time} ms
                        </li>
                      ))}
                    </ul>
                  </CardContent>
                </Card>

                {/* Timing Details */}
                <Card className="col-span-full relative overflow-hidden">
                  <ShineBorder shineColor={["#A07CFE", "#FE8FB5", "#FFBE7B"]} />
                  <CardHeader>
                    <CardTitle>Szczegóły czasów</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <p>Czas parsowania: {response.timing.parseTime} ms</p>
                    <p>
                      Czas dystrybucji: {response.timing.distributionTime} ms
                    </p>
                    <p>
                      Czas konfiguracji zadania: {response.timing.taskSetupTime}{" "}
                      ms
                    </p>
                    <p>
                      Czas przetwarzania: {response.timing.processingTime} ms
                    </p>
                    <p>Całkowity czas: {response.timing.totalTime} ms</p>
                  </CardContent>
                </Card>
              </AnimatedList>
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
